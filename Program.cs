using AiHelpers.Components;
using AiHelpers.Data;
using AiHelpers.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AiHelpers")));

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

builder.Services.AddScoped<ICurrentUserService, DevCurrentUserService>();
builder.Services.AddScoped<ICredentialStore, CredentialStore>();

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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
