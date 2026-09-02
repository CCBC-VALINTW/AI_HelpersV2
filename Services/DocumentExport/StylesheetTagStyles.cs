using System.Text.RegularExpressions;

namespace AiHelpers.Services.DocumentExport;

/// <summary>
/// A handful of tag-level defaults DocxRenderer can apply from a Stylesheet's raw CSS - heading
/// text colour, table border colour, and a base font family. Deliberately not a real CSS engine
/// (no selector specificity/cascade, no combinators beyond "match on the last recognised tag in a
/// descendant chain") - Word export has always been semantic-block-only by design (see
/// DocxRenderer's own doc comment); this is a narrow, best-effort addition on top of that, not a
/// rewrite of it. Every migrated V1 stylesheet scopes its selectors under ".rendDoc" (e.g.
/// ".rendDoc h1", ".rendDoc table") - stripped away here the same as everywhere else, since only
/// the tag actually matters for this.
/// </summary>
internal static partial class StylesheetTagStyles
{
    private static readonly HashSet<string> RecognisedTags =
        new(StringComparer.OrdinalIgnoreCase) { "h1", "h2", "h3", "h4", "h5", "h6", "table", "td", "th", "body", "p" };

    public static TagStyleDefaults Extract(string? css)
    {
        if (string.IsNullOrWhiteSpace(css)) return new TagStyleDefaults(null, null, null);

        var byTag = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (Match rule in RulePattern().Matches(css))
        {
            var declarations = ParseDeclarations(rule.Groups[2].Value);
            if (declarations.Count == 0) continue;

            foreach (var selector in rule.Groups[1].Value.Split(','))
            {
                var tag = LastRecognisedTag(selector);
                if (tag is null) continue;

                if (!byTag.TryGetValue(tag, out var existing))
                {
                    byTag[tag] = existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                foreach (var (property, value) in declarations) existing[property] = value;
            }
        }

        return new TagStyleDefaults(
            HeadingColor: new[] { "h1", "h2", "h3", "h4", "h5", "h6" }
                .Select(tag => GetColor(byTag, tag, "color"))
                .FirstOrDefault(c => c is not null),
            TableBorderColor: new[] { "table", "td", "th" }
                .SelectMany(tag => new[] { GetColor(byTag, tag, "border-color"), GetColor(byTag, tag, "border") })
                .FirstOrDefault(c => c is not null),
            BaseFontFamily: new[] { "body", "p" }
                .Select(tag => GetProperty(byTag, tag, "font-family"))
                .FirstOrDefault(f => f is not null));
    }

    /// <summary>Tries the whole declaration value first - correct (and necessary) for "color:
    /// rgb(200, 50, 10)", where the value IS the colour and naive space-splitting would otherwise
    /// mangle rgb()'s own internal comma/space-separated arguments into unparseable fragments. Only
    /// falls back to splitting on space for a shorthand like "border: 1px solid #ccc", where the
    /// colour is just one of several space-separated tokens (ColorParsing only understands
    /// #hex/rgb(), not "solid"/lengths, so the non-colour tokens harmlessly fail to parse).</summary>
    private static (byte R, byte G, byte B)? GetColor(Dictionary<string, Dictionary<string, string>> byTag, string tag, string property)
    {
        var raw = GetProperty(byTag, tag, property);
        if (raw is null) return null;
        if (ColorParsing.ToRgb(raw) is { } direct) return direct;
        return raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(ColorParsing.ToRgb)
            .FirstOrDefault(c => c is not null);
    }

    private static string? GetProperty(Dictionary<string, Dictionary<string, string>> byTag, string tag, string property) =>
        byTag.TryGetValue(tag, out var props) && props.TryGetValue(property, out var value) ? value.Trim() : null;

    /// <summary>Reduces a selector to whichever recognised tag it's judged to style - the last
    /// recognised tag token in a descendant chain (".rendDoc table td" -&gt; "td", ".rendDoc h1"
    /// -&gt; "h1"), ignoring class/id/combinator tokens along the way. Null for a selector that
    /// never resolves to one of the tags this module cares about - skipped, not guessed at.</summary>
    private static string? LastRecognisedTag(string selector)
    {
        var tokens = selector.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            foreach (var piece in tokens[i].Split('>', ',', '+', '~'))
            {
                var candidate = piece.Trim();
                if (RecognisedTags.Contains(candidate)) return candidate;
            }
        }
        return null;
    }

    private static List<(string Property, string Value)> ParseDeclarations(string block)
    {
        var result = new List<(string, string)>();
        foreach (var decl in block.Split(';'))
        {
            var parts = decl.Split(':', 2);
            if (parts.Length != 2) continue;
            var property = parts[0].Trim().ToLowerInvariant();
            var value = parts[1].Trim();
            if (property.Length > 0 && value.Length > 0) result.Add((property, value));
        }
        return result;
    }

    [GeneratedRegex(@"([^{}]+)\{([^{}]*)\}", RegexOptions.Singleline)]
    private static partial Regex RulePattern();
}

internal sealed record TagStyleDefaults(
    (byte R, byte G, byte B)? HeadingColor,
    (byte R, byte G, byte B)? TableBorderColor,
    string? BaseFontFamily);
