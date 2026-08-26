namespace AiHelpers.Providers;

/// <summary>
/// Decrypted shape of an AwsBedrock ProviderCredential. Never log this type's values.
/// Supports either auth style Bedrock accepts: a bearer token (AWS's newer Bedrock API key
/// feature - a plain Authorization: Bearer header, no request signing) or classic IAM access
/// key/secret (SigV4-signed). Exactly one of BearerToken or AccessKeyId+SecretAccessKey should
/// be set - the adapter picks based on which is present, preferring BearerToken.
/// </summary>
public class AwsCredentialPayload
{
    public required string Region { get; set; }

    public string? BearerToken { get; set; }

    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    /// <summary>Set only for temporary STS credentials, not long-lived IAM user keys.</summary>
    public string? SessionToken { get; set; }
}
