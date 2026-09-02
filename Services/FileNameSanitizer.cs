using System.Text.RegularExpressions;

namespace AiHelpers.Services;

/// <summary>
/// Produces a filename-safe string that's valid across Windows, macOS and Linux simultaneously -
/// not just whatever this app happens to be running on. Path.GetInvalidFileNameChars() only
/// reflects the CURRENT OS's rules (Windows' own reserved characters when running on Windows, as
/// this app always does) and knows nothing about Windows' reserved device names even then.
/// Deliberately conservative: applies the union of every major filesystem's real constraints,
/// since a file produced here may end up opened/saved on any of them. Replaces the two near-
/// identical, narrower ad-hoc versions this app previously had in HelperDetail.razor and
/// Program.cs.
/// </summary>
public static partial class FileNameSanitizer
{
    // Windows' own reserved characters plus control characters - Windows is the strictest of the
    // three major filesystems on this axis (macOS/Linux really only forbid '/' and NUL), so
    // satisfying Windows' rule set satisfies all three.
    private static readonly char[] InvalidChars =
        [.. "<>:\"/\\|?*", .. Enumerable.Range(0, 32).Select(i => (char)i)];

    // Windows reserved device names - illegal as a filename (with or without an extension),
    // case-insensitively, regardless of what else is disallowed.
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    // Comfortably under every real filesystem's ~255-byte/char path-segment limit, even once an
    // extension and multi-byte Unicode encoding are added on top.
    private const int MaxLength = 150;

    public static string Sanitize(string? name, string fallback = "document")
    {
        var cleaned = new string([.. (name ?? "").Select(c => InvalidChars.Contains(c) ? '-' : c)]);
        cleaned = WhitespacePattern().Replace(cleaned, " ");
        cleaned = HyphenRunPattern().Replace(cleaned, "-");
        // Trailing dots/spaces are silently stripped by Windows and can cause a saved file's real
        // name to quietly differ from what was typed - trimmed here so it's consistent everywhere.
        cleaned = cleaned.Trim(' ', '.', '-');

        if (cleaned.Length > MaxLength) cleaned = cleaned[..MaxLength].Trim(' ', '.', '-');

        if (cleaned.Length == 0 || ReservedNames.Contains(cleaned)) return fallback;
        return cleaned;
    }

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex HyphenRunPattern();
}
