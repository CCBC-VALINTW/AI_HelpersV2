using AiHelpers.Data.Enums;

namespace AiHelpers.Data.Entities;

/// <summary>
/// An encrypted credential for calling an LLM provider. The payload shape is provider-specific
/// (e.g. access key/secret/region for AwsBedrock, a single api key for OpenAi) and is stored as
/// encrypted JSON so adding a new provider never needs a schema change.
/// </summary>
public class ProviderCredential
{
    public int Id { get; set; }

    public LlmProvider Provider { get; set; }
    public required string Name { get; set; }
    /// <summary>Whether this is the credential used when a call doesn't ask for one by name.
    /// Enforced as at most one per provider.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Ciphertext from ASP.NET Core Data Protection - a provider-specific JSON payload
    /// (e.g. {"accessKeyId":..,"secretAccessKey":..,"region":..}) once decrypted. Never log or
    /// expose this or its decrypted value outside the credential store/adapter layer.</summary>
    public required string EncryptedPayload { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
