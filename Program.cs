using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
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
// must be requested explicitly, or keys are stored unencrypted.
//
// Certificate-protected, not DPAPI - this app genuinely runs across multiple machines now (Will's
// local dev box plus at least one deployed server), and DPAPI's machine-scoped keys don't roam
// between them. That's not hypothetical: a credential saved from one machine became undecryptable
// on another the first time this actually happened. Every machine that needs to decrypt
// (including local dev) must have the SAME certificate imported into its own LocalMachine\My
// store, private key included - see docs/DEPLOYMENT.md for generation/distribution steps.
// Deliberately no DPAPI fallback if the thumbprint isn't configured - failing loudly on a
// missing/misconfigured cert beats silently reverting to the per-machine mode this exists to
// get away from. Must stay in sync with Tools/CredentialManager's own Data Protection setup
// (same PersistKeysToDbContext target, same certificate, same application name) or credentials
// encrypted by one won't decrypt in the other.
var dataProtectionCertThumbprint = builder.Configuration["DataProtection:CertificateThumbprint"];
if (string.IsNullOrWhiteSpace(dataProtectionCertThumbprint) || dataProtectionCertThumbprint == "CHANGE_ME")
{
    throw new InvalidOperationException(
        "DataProtection:CertificateThumbprint is not configured - see docs/DEPLOYMENT.md for how to generate and install the shared certificate.");
}
// The string-thumbprint overload of ProtectKeysWithCertificate uses its own built-in resolver,
// which failed to find a certificate confirmed (via Get-ChildItem) to genuinely be sitting in
// LocalMachine\My, readable from a normal non-elevated session - not worth relying on undocumented
// default store-search behaviour. Looking it up explicitly from the exact store instead removes
// the ambiguity entirely.
X509Certificate2 dataProtectionCert;
using (var certStore = new X509Store(StoreName.My, StoreLocation.LocalMachine))
{
    certStore.Open(OpenFlags.ReadOnly);
    var found = certStore.Certificates.Find(X509FindType.FindByThumbprint, dataProtectionCertThumbprint, validOnly: false);
    if (found.Count == 0)
    {
        throw new InvalidOperationException(
            $"Certificate with thumbprint {dataProtectionCertThumbprint} was not found in LocalMachine\\My - see docs/DEPLOYMENT.md.");
    }
    dataProtectionCert = found[0];
}
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .ProtectKeysWithCertificate(dataProtectionCert)
    .SetApplicationName("AiHelpers");

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

// Stateless (no per-request/per-circuit dependencies) - the docx/pdf renderers it wraps only take
// plain strings in and bytes out - so a singleton is fine, same as any other pure converter.
builder.Services.AddSingleton<IDocumentExportService, DocumentExportService>();

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

// Word/HTML file downloads for the document editor (Components/Pages/Documents/
// DocumentEditor.razor) - a plain HTTP GET endpoint rather than a Blazor Server data: URI download
// link (the pattern HelperDetail.razor uses for its own HTML-only "Download as HTML" button),
// since a docx has to be actual binary bytes with the right Content-Type, not something that fits
// neatly into a data: URI the way small HTML output does. PDF export doesn't come through here at
// all any more - see DocumentEditor.razor's PrintAsync/structuredEditor.js's printDocument(), which
// triggers the browser's own native print-to-PDF against the live preview iframe client-side
// instead. No [AllowAnonymous]/explicit
// [Authorize] needed - this endpoint has no authorization metadata of its own, so the global
// FallbackPolicy above (RequireAuthenticatedUser) applies to it exactly the same as it does to
// every Razor/MVC page. HttpContext.User - not ICurrentUserService - is used here deliberately:
// ICurrentUserService reads AuthenticationStateProvider specifically because a Blazor Server
// circuit outlives the original HTTP request, but this endpoint IS a plain, single HTTP request
// (a real browser navigation, not SignalR/circuit traffic), so HttpContext.User already reflects
// the signed-in user directly - see ICurrentUserService's own doc comment for that distinction.
app.MapGet("/documents/{id:int}/export/{format}", async (
    int id,
    string format,
    AppDbContext db,
    IDocumentExportService exportService,
    HttpContext http) =>
{
    var email = http.User.FindFirstValue(ClaimTypes.Email)
        ?? http.User.FindFirstValue("preferred_username")
        ?? http.User.FindFirstValue(ClaimTypes.Upn);

    var document = await db.GeneratedDocuments.Include(d => d.Stylesheet).FirstOrDefaultAsync(d => d.Id == id);

    // Same "not found" response whether the document doesn't exist or belongs to someone else -
    // documents are private to whoever sent them to the editor (no sharing/admin-override in this
    // first pass, see the project's build report), so this deliberately doesn't distinguish the
    // two cases to a caller probing IDs.
    if (document is null || email is null || !string.Equals(document.CreatedByEmail, email, StringComparison.OrdinalIgnoreCase))
    {
        return Results.NotFound();
    }

    var fileName = FileNameSanitizer.Sanitize(document.Title);

    return format switch
    {
        "docx" => Results.File(
            exportService.ToDocx(document.Title, document.HtmlContent, document.Stylesheet?.Css),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"{fileName}.docx"),
        "html" => Results.File(
            exportService.ToHtml(document.Title, document.HtmlContent, document.Stylesheet?.Css),
            "text/html",
            $"{fileName}.html"),
        _ => Results.BadRequest("Unsupported export format - expected docx or html."),
    };
});

app.Run();
