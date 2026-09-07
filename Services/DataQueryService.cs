using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AiHelpers.Data.Entities;
using AiHelpers.Data.Enums;
using Microsoft.AspNetCore.DataProtection;

namespace AiHelpers.Services;

public class DataQueryService : IDataQueryService
{
    // 30s - generous enough for a real analytical query, short enough that a Helper run can't
    // hang indefinitely on a stuck connection. Same "bounded ceiling, not unlimited" reasoning as
    // UrlFetchService's HttpClient timeout.
    private const int CommandTimeoutSeconds = 30;

    private readonly IDataProtector _protector;

    public DataQueryService(IDataProtectionProvider dataProtectionProvider)
    {
        // Separate purpose from CredentialStore's own protector - independent encryption context,
        // no reason for the two to share one. Versioned constant, same reasoning as CredentialStore.
        _protector = dataProtectionProvider.CreateProtector("AiHelpers.DataConnections.v1");
    }

    public string EncryptConnectionString(string connectionString) => _protector.Protect(connectionString);

    public async Task<DataQueryResult> TestAsync(DataConnection connection, CancellationToken cancellationToken = default) =>
        await ExecuteAsync(connection, "SELECT 1", maxRows: 1, DataQueryOutputFormat.Csv, compactionEnabled: true, cancellationToken);

    public async Task<DataQueryResult> ExecuteAsync(DataConnection connection, string query, int maxRows, DataQueryOutputFormat format, bool compactionEnabled, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var connectionString = _protector.Unprotect(connection.EncryptedConnectionString);

            await using var odbcConnection = new OdbcConnection(connectionString);
            await odbcConnection.OpenAsync(cancellationToken);

            await using var command = odbcConnection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = CommandTimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            var rows = new List<object?[]>();
            var truncated = false;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count >= maxRows)
                {
                    truncated = true;
                    break;
                }
                var values = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    values[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(values);
            }

            // Both representations are always computed, regardless of compactionEnabled - a real
            // Helper run with the toggle off never looks at EstimatedTokensAfterCompaction, but
            // the extra formatting pass over rows already in memory is cheap enough (bounded by
            // MaxRows) not to bother special-casing it out, and it lets the Helper Editor's own
            // "Test query" preview show the potential saving even while previewing with the
            // toggle switched off.
            var flatContent = format == DataQueryOutputFormat.Json
                ? FormatFlatJson(columns, rows, truncated)
                : FormatFlatCsv(columns, rows, truncated);
            var compactedContent = format == DataQueryOutputFormat.Json
                ? FormatCompactedJson(columns, rows, truncated)
                : FormatCompactedCsv(columns, rows, truncated);

            stopwatch.Stop();
            return new DataQueryResult
            {
                Success = true,
                Content = compactionEnabled ? compactedContent : flatContent,
                RowCount = rows.Count,
                Truncated = truncated,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                EstimatedTokensBeforeCompaction = EstimateTokens(flatContent),
                EstimatedTokensAfterCompaction = EstimateTokens(compactedContent)
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // Real errors (bad query, connection down, permission denied) are an expected,
            // everyday outcome here, not exceptional - surfaced verbatim, same "let the real
            // system tell us" approach as BedrockAdapter's own error handling.
            return new DataQueryResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>The original, always-flat CSV shape - one header row, one line per row, no
    /// grouping - used only as the "before" side of the Helper Editor's token estimate.</summary>
    private static string FormatFlatCsv(string[] columns, List<object?[]> rows, bool truncated)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', columns.Select(EscapeCsvField)));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',', row.Select(v => EscapeCsvField(FormatValue(v)))));
        }
        AppendTruncationNotice(sb, truncated, rows.Count);
        return sb.ToString();
    }

    /// <summary>The real, sent-to-the-model shape - see DataResultCompactor's own doc comment for
    /// the collapsing rule. Degrades to identical output to FormatFlatCsv above when nothing in
    /// the result is actually compactable.</summary>
    private static string FormatCompactedCsv(string[] columns, List<object?[]> rows, bool truncated)
    {
        var compacted = DataResultCompactor.Compact(columns, rows);
        var sb = new StringBuilder();

        if (compacted.GroupColumns.Length == 0)
        {
            sb.AppendLine(string.Join(',', compacted.DetailColumns.Select(EscapeCsvField)));
            foreach (var row in compacted.Groups[0].DetailRows)
            {
                sb.AppendLine(string.Join(',', row.Select(v => EscapeCsvField(FormatValue(v)))));
            }
        }
        else
        {
            foreach (var group in compacted.Groups)
            {
                var header = string.Join(" | ", compacted.GroupColumns.Zip(group.GroupValues,
                    (col, val) => $"{col}: {FormatValue(val)}"));
                sb.AppendLine($"## {header}");
                sb.AppendLine(string.Join(',', compacted.DetailColumns.Select(EscapeCsvField)));
                foreach (var row in group.DetailRows)
                {
                    sb.AppendLine(string.Join(',', row.Select(v => EscapeCsvField(FormatValue(v)))));
                }
            }
        }

        AppendTruncationNotice(sb, truncated, rows.Count);
        return sb.ToString();
    }

    private static void AppendTruncationNotice(StringBuilder sb, bool truncated, int rowCount)
    {
        if (truncated)
        {
            sb.AppendLine($"[truncated - only the first {rowCount} row(s) shown]");
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (field.IndexOfAny([',', '"', '\r', '\n']) < 0) return field;
        return $"\"{field.Replace("\"", "\"\"")}\"";
    }

    private static string FormatFlatJson(string[] columns, List<object?[]> rows, bool truncated)
    {
        var array = rows.Select(row => ToDict(columns, row)).ToList();
        return SerializeJsonWrapper(array, truncated);
    }

    /// <summary>Same collapsing rule as FormatCompactedCsv, expressed as real JSON nesting
    /// (group columns plus a "detail" array) rather than CSV's repeated header-block convention -
    /// a more natural fit for JSON specifically. Degrades to an identical flat array to
    /// FormatFlatJson above when nothing is compactable.</summary>
    private static string FormatCompactedJson(string[] columns, List<object?[]> rows, bool truncated)
    {
        var compacted = DataResultCompactor.Compact(columns, rows);

        object array = compacted.GroupColumns.Length == 0
            ? compacted.Groups[0].DetailRows.Select(row => ToDict(compacted.DetailColumns, row)).ToList()
            : compacted.Groups.Select(g =>
            {
                var obj = ToDict(compacted.GroupColumns, g.GroupValues);
                obj["detail"] = g.DetailRows.Select(row => ToDict(compacted.DetailColumns, row)).ToList();
                return obj;
            }).ToList();

        return SerializeJsonWrapper(array, truncated);
    }

    private static string SerializeJsonWrapper(object rowsOrGroups, bool truncated)
    {
        var wrapper = new Dictionary<string, object?>
        {
            ["rows"] = rowsOrGroups,
            ["truncated"] = truncated
        };
        return JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = false });
    }

    private static Dictionary<string, object?> ToDict(string[] columns, object?[] values)
    {
        var obj = new Dictionary<string, object?>();
        for (var i = 0; i < columns.Length; i++)
        {
            obj[columns[i]] = values[i];
        }
        return obj;
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "",
        DateTime dt => dt.ToString("O"),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
    };

    // A well-known, widely-used rough approximation (~4 characters per token for English-ish
    // text) - deliberately not a real tokenizer. Bedrock only returns actual token counts after a
    // real Converse call, which would mean spending real money and requiring a configured
    // credential just to preview a Data Source - not worth it for what's meant to be a free,
    // instant estimate. Good enough to show the shape of the saving, not exact to the token.
    private static int EstimateTokens(string text) => (int)Math.Ceiling(text.Length / 4.0);
}
