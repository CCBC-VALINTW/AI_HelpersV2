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
        await ExecuteAsync(connection, "SELECT 1", maxRows: 1, DataQueryOutputFormat.Csv, cancellationToken);

    public async Task<DataQueryResult> ExecuteAsync(DataConnection connection, string query, int maxRows, DataQueryOutputFormat format, CancellationToken cancellationToken = default)
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

            var content = format == DataQueryOutputFormat.Json
                ? FormatJson(columns, rows, truncated)
                : FormatCsv(columns, rows, truncated);

            stopwatch.Stop();
            return new DataQueryResult
            {
                Success = true,
                Content = content,
                RowCount = rows.Count,
                Truncated = truncated,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
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

    private static string FormatCsv(string[] columns, List<object?[]> rows, bool truncated)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', columns.Select(EscapeCsvField)));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',', row.Select(v => EscapeCsvField(FormatValue(v)))));
        }
        if (truncated)
        {
            sb.AppendLine($"[truncated - only the first {rows.Count} row(s) shown]");
        }
        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.IndexOfAny([',', '"', '\r', '\n']) < 0) return field;
        return $"\"{field.Replace("\"", "\"\"")}\"";
    }

    private static string FormatJson(string[] columns, List<object?[]> rows, bool truncated)
    {
        var array = rows.Select(row =>
        {
            var obj = new Dictionary<string, object?>();
            for (var i = 0; i < columns.Length; i++)
            {
                obj[columns[i]] = row[i];
            }
            return obj;
        }).ToList();

        var wrapper = new Dictionary<string, object?>
        {
            ["rows"] = array,
            ["truncated"] = truncated
        };
        return JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = false });
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "",
        DateTime dt => dt.ToString("O"),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
    };
}
