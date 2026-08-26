using AiHelpers.Data.Enums;

namespace AiHelpers.Services;

/// <summary>
/// Encrypted storage for provider credentials (AWS keys, future OpenAI/Minimax API keys, etc.).
/// Payloads are provider-specific POCOs serialized to JSON and encrypted with ASP.NET Core Data
/// Protection before hitting the database - callers never see ciphertext directly.
/// </summary>
public interface ICredentialStore
{
    Task<T?> GetDefaultAsync<T>(LlmProvider provider, CancellationToken cancellationToken = default) where T : class;

    Task SetDefaultAsync<T>(LlmProvider provider, string name, T payload, string createdBy, CancellationToken cancellationToken = default) where T : class;
}
