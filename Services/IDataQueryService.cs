using AiHelpers.Data.Entities;
using AiHelpers.Data.Enums;

namespace AiHelpers.Services;

public class DataQueryResult
{
    public required bool Success { get; init; }
    /// <summary>The formatted CSV/JSON text, ready to fold straight into a Helper's input. Null
    /// when Success is false.</summary>
    public string? Content { get; init; }
    public int RowCount { get; init; }
    /// <summary>True when the underlying result had more rows than the configured cap and was cut
    /// short - always stated explicitly in Content too, never silently.</summary>
    public bool Truncated { get; init; }
    public int DurationMs { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Rough ~2-chars-per-token estimates (not a real tokenizer - see
    /// DataQueryService.EstimateTokens), before vs. after DataResultCompactor's denormalised-join
    /// collapsing. Only the "after" figure is ever actually sent to the model (that's Content) -
    /// "before" exists purely so the Helper Editor's Test query preview can show the real saving,
    /// query by query. Both 0 when Success is false.</summary>
    public int EstimatedTokensBeforeCompaction { get; init; }
    public int EstimatedTokensAfterCompaction { get; init; }
}

/// <summary>
/// Executes a HelperDataQuery.Query against a DataConnection and formats the result as CSV or
/// JSON text. Deliberately returns a result object rather than throwing on a query failure - a
/// bad/failing query is an expected, everyday outcome here (wrong column name, connection down),
/// not an exceptional one, same reasoning as HelperInvocationOutcome elsewhere in this app.
/// </summary>
public interface IDataQueryService
{
    /// <summary>
    /// <paramref name="compactionEnabled"/> controls only which of the two already-computed
    /// representations Content actually holds - EstimatedTokensBeforeCompaction/
    /// AfterCompaction are always both populated regardless, so a caller previewing a query can
    /// show the potential saving even while the toggle is off.
    /// </summary>
    Task<DataQueryResult> ExecuteAsync(DataConnection connection, string query, int maxRows, DataQueryOutputFormat format, bool compactionEnabled, CancellationToken cancellationToken = default);

    /// <summary>Connectivity-only check (a fixed trivial SELECT) for the admin page's own "Test
    /// connection" action - shares this same execution path rather than duplicating
    /// connection-opening logic.</summary>
    Task<DataQueryResult> TestAsync(DataConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Used by the admin page when saving a new/updated connection string. Kept behind
    /// this same service (rather than the admin page creating its own IDataProtector) so the
    /// encryption purpose string exists in exactly one place - a mismatch between where something
    /// is encrypted and where it's decrypted is a real, previously-hit failure mode in this app
    /// (see the Data Protection/DPAPI-to-certificate history).</summary>
    string EncryptConnectionString(string connectionString);
}
