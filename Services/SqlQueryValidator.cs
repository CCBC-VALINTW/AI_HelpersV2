using System.Text.RegularExpressions;

namespace AiHelpers.Services;

/// <summary>
/// A deterministic shape-check on an admin/owner-authored HelperDataQuery.Query before it's saved
/// or run - catches honest mistakes and unsophisticated attempts (a query that plainly starts
/// with DELETE, or stacks a second statement after a semicolon). This is NOT the real security
/// boundary and was never meant to be: a keyword scan is bypassable by a sufficiently determined
/// author (a stored procedure call that doesn't syntactically look like a write, dialect-specific
/// syntax this doesn't know about), and this app connects to many different ODBC dialects with no
/// single real SQL parser that understands all of them. The actual enforcement is - and must
/// remain - that every DataConnection authenticates as a dedicated, least-privilege account on
/// its target system. Treat this as a lint, not a gate that makes a risky connection safe.
/// </summary>
public static partial class SqlQueryValidator
{
    private static readonly string[] DangerousKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE", "MERGE",
        "EXEC", "EXECUTE", "GRANT", "REVOKE", "DENY", "CREATE", "sp_executesql"
    ];

    /// <summary>Null means the query passed every check. Returns the first problem found, not an
    /// exhaustive list - good enough for a save-time nudge, not worth the complexity of collecting
    /// every issue at once.</summary>
    public static string? Validate(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Query is required.";

        // Comments and string literal contents are stripped before shape-checking, so a keyword
        // inside a comment or a quoted value (e.g. WHERE status = 'DELETED') doesn't false-positive,
        // and so a semicolon inside a string literal isn't mistaken for statement-stacking.
        var stripped = StripCommentsAndLiterals(query);

        var trimmed = stripped.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return "Only a single SELECT (or WITH ... SELECT) statement is allowed - the query must start with SELECT or WITH.";
        }

        // A single trailing semicolon (optionally followed by whitespace) is fine; anything else
        // after one means a second statement is being stacked on.
        var semicolon = stripped.IndexOf(';');
        if (semicolon >= 0 && !string.IsNullOrWhiteSpace(stripped[(semicolon + 1)..]))
        {
            return "Only one statement is allowed - remove whatever follows the semicolon.";
        }

        foreach (var keyword in DangerousKeywords)
        {
            if (KeywordRegex(keyword).IsMatch(stripped))
            {
                return $"\"{keyword}\" isn't allowed here - this must be a read-only query. If this is a false positive (e.g. the word appears in a column/table name), rephrase using a quoted identifier.";
            }
        }

        return null;
    }

    private static string StripCommentsAndLiterals(string query)
    {
        var noBlockComments = BlockCommentPattern().Replace(query, " ");
        var noLineComments = LineCommentPattern().Replace(noBlockComments, " ");
        return StringLiteralPattern().Replace(noLineComments, "''");
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentPattern();

    [GeneratedRegex(@"--[^\r\n]*")]
    private static partial Regex LineCommentPattern();

    // Matches a whole '...' literal, including '' as an escaped quote inside one.
    [GeneratedRegex(@"'(?:[^']|'')*'")]
    private static partial Regex StringLiteralPattern();

    private static Regex KeywordRegex(string keyword) => new($@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase);
}
