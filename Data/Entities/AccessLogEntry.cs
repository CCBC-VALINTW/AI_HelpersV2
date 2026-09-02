namespace AiHelpers.Data.Entities;

/// <summary>
/// Lightweight "who's opening this and how often" visibility, not a security audit trail (Will's
/// explicit scoping - no page/path, no IP, no user agent, just email + when). One row per real
/// page load, mirroring MainLayout's own "OnInitializedAsync only fires once per circuit" behaviour
/// - not one row per in-app navigation. See AccessLogService for the write path and its rolling
/// retention/cleanup.
/// </summary>
public class AccessLogEntry
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
