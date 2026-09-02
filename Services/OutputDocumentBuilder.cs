using System.Net;
using System.Text.RegularExpressions;

namespace AiHelpers.Services;

/// <summary>
/// Wraps model output as a standalone HTML document for a sandboxed iframe - shared between the
/// real run page (HelperDetail.razor) and the Helper Editor's preview facility, so the stylesheet
/// application/stripping rules can't drift between "the real thing" and "the preview of it".
/// See HelperDetail.razor's original BuildOutputDocument for the full rationale (rendDoc wrapper
/// class, embedded-style stripping, why this happens at render time rather than invocation time).
/// </summary>
public static class OutputDocumentBuilder
{
    /// <summary>
    /// A stylesheet row can exist purely as a placeholder (V1's migrated "No Stylesheet" row is
    /// literally the empty tag "&lt;style&gt;&lt;/style&gt;") - checked by content, not by name.
    /// </summary>
    public static bool HasRealStyling(string? css) =>
        !string.IsNullOrWhiteSpace(css) &&
        !string.IsNullOrWhiteSpace(Regex.Replace(css, "</?style[^>]*>", "", RegexOptions.IgnoreCase));

    /// <summary>
    /// <paramref name="title"/> is optional - the iframe preview/data-uri callers don't need a
    /// document &lt;title&gt;, only the exported-HTML-file caller (DocumentExportService) does.
    /// </summary>
    public static string Build(string bodyHtml, string? stylesheetCss, string? title = null)
    {
        var applyStylesheet = HasRealStyling(stylesheetCss);
        var body = applyStylesheet ? StripEmbeddedStyles(bodyHtml) : bodyHtml;
        var titleTag = title is null ? "" : $"<title>{WebUtility.HtmlEncode(title)}</title>\n";

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8" />
            {{titleTag}}<style>body { font-family: Calibri, Arial, sans-serif; margin: 1rem; }</style>
            {{(applyStylesheet ? stylesheetCss : "")}}
            </head>
            <body><div class="rendDoc">{{body}}</div></body>
            </html>
            """;
    }

    private static string StripEmbeddedStyles(string html) =>
        Regex.Replace(html, "<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
}
