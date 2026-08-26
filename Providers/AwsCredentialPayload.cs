namespace AiHelpers.Providers;

/// <summary>Decrypted shape of an AwsBedrock ProviderCredential. Never log this type's values.</summary>
public class AwsCredentialPayload
{
    public required string AccessKeyId { get; set; }
    public required string SecretAccessKey { get; set; }
    public required string Region { get; set; }
    /// <summary>Set only for temporary STS credentials, not long-lived IAM user keys.</summary>
    public string? SessionToken { get; set; }
}
