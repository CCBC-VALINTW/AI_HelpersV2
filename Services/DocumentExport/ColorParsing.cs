using System.Text.RegularExpressions;

namespace AiHelpers.Services.DocumentExport;

/// <summary>
/// Reads the colours HtmlContentSanitizer lets through inline "style" attributes. TinyMCE's
/// forecolor/backcolor toolbar buttons write CSS colour values as rgba(r, g, b, a) (confirmed
/// against what actually survives sanitization, not just the editor's own output - see the
/// project's build report), but #hex is handled too since it's valid CSS a paste could carry.
/// </summary>
internal static partial class ColorParsing
{
    public static (byte R, byte G, byte B)? ToRgb(string cssColor)
    {
        cssColor = cssColor.Trim();

        if (cssColor.StartsWith('#'))
        {
            var hex = cssColor[1..];
            if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
            if (hex.Length != 6) return null;
            return byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) &&
                   byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) &&
                   byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b)
                ? (r, g, b)
                : null;
        }

        var match = RgbPattern().Match(cssColor);
        if (!match.Success) return null;

        return (byte.Parse(match.Groups[1].Value), byte.Parse(match.Groups[2].Value), byte.Parse(match.Groups[3].Value));
    }

    [GeneratedRegex(@"rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*(?:,\s*[\d.]+\s*)?\)", RegexOptions.IgnoreCase)]
    private static partial Regex RgbPattern();
}
