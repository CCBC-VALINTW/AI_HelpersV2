using System.Security.Claims;
using AiHelpers.Components;
using AiHelpers.Data;
using AiHelpers.Providers;
using AiHelpers.Providers.Bedrock;
using AiHelpers.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Blazor Server's SignalR circuit defaults to a 32KB max message size. JS interop payloads count
// against it - e.g. reading a rich-text editor's full HTML content back to the server - so
// pasting a large document (a full policy document as formatted HTML easily exceeds 32KB) made
// the circuit silently disconnect rather than throw a visible error. Raised, not removed, since
// this is still a resource-exhaustion guard against an unbounded payload from an authenticated
// client, not just Blazor plumbing to unblock.
builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 5 * 1024 * 1024);

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    // Lets the app exchange the sign-in token for a Graph access token (User.Read, delegated,
    // own-profile only) so the "organisational info" pre-fill can read jobTitle/department/
    // officeLocation instead of relying purely on manual entry - see
    // GraphOrganisationalInfoService. In-memory cache is fine for a single on-prem server, same
    // reasoning as the DPAPI data-protection key ring above.
    .EnableTokenAcquisitionToCallDownstreamApi(["User.Read"])
    .AddInMemoryTokenCaches();
builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
builder.Services.AddCascadingAuthenticationState();

var adminEmails = builder.Configuration.GetSection("AdminEmails").Get<string[]>() ?? [];
builder.Services.AddAuthorization(options =>
{
    // Every page requires sign-in by default; pages needing anonymous access opt out with
    // [AllowAnonymous] rather than the reverse, so nothing is accidentally left open.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("AdminOnly", policy => policy.RequireAssertion(context =>
    {
        var email = context.User.FindFirstValue(ClaimTypes.Email)
            ?? context.User.FindFirstValue("preferred_username")
            ?? context.User.FindFirstValue(ClaimTypes.Upn);
        return email is not null && adminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
    }));
});

// Registered as a factory, not plain AddDbContext - SpendStatusService (used from
// MainLayout.OnInitializedAsync) needs its own independent, short-lived context rather than the
// single circuit-scoped instance every page's @inject AppDbContext Db shares, since Blazor Server
// doesn't serialize a layout's async init against its routed page's own - both are part of the
// same initial render pass and genuinely race on one shared DbContext otherwise (see
// project_ai_helpers_llm_service memory for the concurrency exception this caused).
// AddScoped<AppDbContext> below derives the plain injectable-per-scope registration from the same
// factory - this is EF Core's documented pattern for needing both AddDbContext and
// AddDbContextFactory together; calling them as two separate, independently-configured
// registrations (what shipped first) creates two conflicting DbContextOptions<AppDbContext>
// registrations (scoped vs singleton) and fails at startup.
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AiHelpers")));
builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// Encrypted at rest via ASP.NET Core Data Protection; key ring lives in AI_Helpers so it's
// shared between this app and Tools/CredentialManager. PersistKeysToDbContext does NOT get
// automatic at-rest protection the way the default filesystem repository does on Windows - it
// must be requested explicitly, or keys are stored unencrypted. DPAPI machine-scope is right
// for a single on-prem server; if this ever runs across multiple servers, switch to
// ProtectKeysWithCertificate instead, since DPAPI machine keys don't roam.
// This app is only ever deployed to Windows Server (Conwy's on-prem infrastructure).
#pragma warning disable CA1416
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .ProtectKeysWithDpapi(protectToLocalMachine: true)
    .SetApplicationName("AiHelpers");
#pragma warning restore CA1416

builder.Services.AddScoped<ICurrentUserService, EntraCurrentUserService>();
builder.Services.AddScoped<ICredentialStore, CredentialStore>();

builder.Services.AddHttpClient<IOrganisationalInfoService, GraphOrganisationalInfoService>(client =>
    client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"));

// AllowAutoRedirect is off deliberately - UrlFetchService follows redirects itself so it can
// validate each hop against the SSRF allow/block rules (see its own doc comment), rather than
// letting HttpClient silently follow a redirect straight into a blocked address.
builder.Services.AddHttpClient<IUrlFetchService, UrlFetchService>(client => client.Timeout = TimeSpan.FromSeconds(20))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

// HttpClient's own default (100s) is nowhere near enough - real Helper calls have been observed
// taking 20+ minutes for long documents. This is still a bounded ceiling, not unlimited, since an
// actually-hung request should eventually give up rather than tie up the call indefinitely.
// Genuinely long-running calls are also why streaming (Bedrock's ConverseStream) is the more
// complete answer long-term - see project_ai_helpers_llm_service memory - but that's a separate,
// larger piece of work, not a substitute for having a sane timeout regardless.
//
// Registered against ILlmProviderAdapter directly (AddHttpClient<TInterface, TImplementation>),
// not AddHttpClient<BedrockAdapter>() + a separate AddScoped<ILlmProviderAdapter, BedrockAdapter>
// - that pairing looks equivalent but isn't: it creates two independent construction paths for
// BedrockAdapter, and only the one resolving BedrockAdapter directly (not via the interface) goes
// through the properly-configured named HttpClient. HelperInvocationService only ever resolves
// via the interface (IEnumerable<ILlmProviderAdapter>), so it was silently getting a second,
// separately-constructed BedrockAdapter with a plain, unconfigured HttpClient stuck on .NET's
// 100s default the whole time - a latent bug since this was first wired up, not something the
// 30-minute timeout change introduced.
builder.Services.AddHttpClient<ILlmProviderAdapter, BedrockAdapter>(client => client.Timeout = TimeSpan.FromMinutes(30));
builder.Services.AddScoped<IHelperInvocationService, HelperInvocationService>();

// Scoped, not singleton - one instance per circuit (per signed-in user), so the top status bar's
// spend/cap figures never leak between users sharing the same server process.
builder.Services.AddScoped<ISpendStatusService, SpendStatusService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// The global FallbackPolicy would otherwise block /MicrosoftIdentity/Account/SignIn itself
// before its own sign-in logic ever runs - the one place anonymous access is actually correct.
app.MapControllers().AllowAnonymous();

app.Run();
