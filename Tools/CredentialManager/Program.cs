// Sets an encrypted provider credential (e.g. AWS Bedrock keys) in the AI_Helpers database.
// Secrets are prompted for interactively (masked input) rather than passed as CLI args, so
// they never land in shell history or a process list. Not a web page on purpose - this app has
// no real authentication yet, so a credential-entry web form would be reachable by anyone.
//
// Usage:
//   dotnet run -- --target "<connection string>" --provider AwsBedrock --created-by "<email>"
//   dotnet run -- --target "<connection string>" --provider AwsBedrock --verify

using AiHelpers.Data;
using AiHelpers.Data.Enums;
using AiHelpers.Providers;
using AiHelpers.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var options = ParseArgs(args);
if (options is null) return 1;

var services = new ServiceCollection();
services.AddDbContext<AppDbContext>(o => o.UseSqlServer(options.Value.TargetConnectionString));

// Must match Program.cs in the main app exactly (same PersistKeysToDbContext target, same
// DPAPI scope, same application name) or credentials encrypted here won't decrypt there.
#pragma warning disable CA1416
services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .ProtectKeysWithDpapi(protectToLocalMachine: true)
    .SetApplicationName("AiHelpers");
#pragma warning restore CA1416

services.AddScoped<ICredentialStore, CredentialStore>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var store = scope.ServiceProvider.GetRequiredService<ICredentialStore>();

if (options.Value.Verify)
{
    return await VerifyAsync(store, options.Value.Provider);
}

switch (options.Value.Provider)
{
    case LlmProvider.AwsBedrock:
        await SetAwsCredentialAsync(store, options.Value.CreatedBy!);
        break;
    default:
        Console.Error.WriteLine($"No credential entry flow implemented yet for {options.Value.Provider}.");
        return 1;
}

Console.WriteLine("Credential saved (encrypted).");
return 0;

static async Task<int> VerifyAsync(ICredentialStore store, LlmProvider providerName)
{
    if (providerName != LlmProvider.AwsBedrock)
    {
        Console.Error.WriteLine($"No verify flow implemented yet for {providerName}.");
        return 1;
    }

    var payload = await store.GetDefaultAsync<AwsCredentialPayload>(providerName);
    if (payload is null)
    {
        Console.WriteLine($"No default credential stored for {providerName}.");
        return 1;
    }

    // Decrypts successfully and shows just enough to confirm it's the right one - never the
    // secret itself.
    Console.WriteLine($"Provider: {providerName}");
    Console.WriteLine($"AccessKeyId: {Mask(payload.AccessKeyId)}");
    Console.WriteLine($"Region: {payload.Region}");
    Console.WriteLine($"SecretAccessKey: set, {payload.SecretAccessKey.Length} characters");
    Console.WriteLine($"SessionToken: {(payload.SessionToken is null ? "not set" : $"set, {payload.SessionToken.Length} characters")}");
    return 0;

    static string Mask(string value) => value.Length <= 4 ? "****" : value[..4] + new string('*', value.Length - 4);
}

static async Task SetAwsCredentialAsync(ICredentialStore store, string createdBy)
{
    Console.Write("AWS Access Key ID: ");
    var accessKeyId = (Console.ReadLine() ?? "").Trim();

    var secretAccessKey = ReadMasked("AWS Secret Access Key: ");

    Console.Write("AWS Region (e.g. eu-west-2): ");
    var region = (Console.ReadLine() ?? "").Trim();

    var sessionToken = ReadMasked("Session token (blank if using a long-lived IAM user key): ");

    var payload = new AwsCredentialPayload
    {
        AccessKeyId = accessKeyId,
        SecretAccessKey = secretAccessKey,
        Region = region,
        SessionToken = string.IsNullOrWhiteSpace(sessionToken) ? null : sessionToken
    };

    await store.SetDefaultAsync(LlmProvider.AwsBedrock, "Default", payload, createdBy);
}

static string ReadMasked(string prompt)
{
    Console.Write(prompt);

    // Console.ReadKey requires a real interactive terminal - fall back to a plain read when
    // input is piped/redirected (e.g. scripted credential injection).
    if (Console.IsInputRedirected)
    {
        return (Console.ReadLine() ?? "").Trim();
    }

    var value = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace)
        {
            if (value.Length == 0) continue;
            value.Length--;
            Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
            value.Append(key.KeyChar);
            Console.Write('*');
        }
    }
    Console.WriteLine();
    return value.ToString();
}

static (string TargetConnectionString, LlmProvider Provider, string? CreatedBy, bool Verify)? ParseArgs(string[] args)
{
    string? targetConn = null, providerName = null, createdBy = null;
    var verify = false;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--target" when i + 1 < args.Length:
                targetConn = args[++i];
                break;
            case "--provider" when i + 1 < args.Length:
                providerName = args[++i];
                break;
            case "--created-by" when i + 1 < args.Length:
                createdBy = args[++i];
                break;
            case "--verify":
                verify = true;
                break;
        }
    }

    if (targetConn is null || providerName is null || (!verify && createdBy is null) || !Enum.TryParse<LlmProvider>(providerName, out var provider))
    {
        Console.Error.WriteLine("Usage: dotnet run -- --target \"<connection string>\" --provider AwsBedrock --created-by \"<email>\"");
        Console.Error.WriteLine("       dotnet run -- --target \"<connection string>\" --provider AwsBedrock --verify");
        return null;
    }

    return (targetConn, provider, createdBy, verify);
}
