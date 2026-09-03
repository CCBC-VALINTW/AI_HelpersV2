namespace AiHelpers.Data.Entities;

/// <summary>
/// One row per HelperDataQuery execution attempt (success or failure), regardless of whether the
/// Helper run itself ultimately succeeded - same auditability instinct as AccountingEntry/
/// AccessLogEntry/CallbackEntry elsewhere in this app. Exists purely for "why did this Helper's
/// output look wrong" debugging and security review, never read by the run itself.
/// </summary>
public class DataQueryExecutionLog
{
    public int Id { get; set; }

    /// <summary>Not a required FK - HelperDataQuery can be deleted (an admin removes it from the
    /// Helper, or the whole Helper is deleted) while its execution history stays, same SetNull
    /// reasoning AccountingEntry/CallbackEntry already use for HelperDefinitionId.</summary>
    public int? HelperDataQueryId { get; set; }
    public HelperDataQuery? HelperDataQuery { get; set; }

    /// <summary>Snapshotted at execution time, kept even if the query/connection is later edited
    /// or removed - same reasoning as AccountingEntry.HelperName.</summary>
    public required string Label { get; set; }
    public string? UserId { get; set; }

    public bool Succeeded { get; set; }
    public int? RowCount { get; set; }
    public bool Truncated { get; set; }
    public int DurationMs { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
