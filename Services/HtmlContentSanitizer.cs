using Ganss.Xss;

namespace AiHelpers.Services;

/// <summary>
/// Sanitizes untrusted HTML (an LLM's raw output) before it's placed into the host page's own
/// DOM. Only needed for in-place rich-text editing - the read-only preview stays inside a
/// sandboxed iframe and never needs this. Contenteditable requires a real same-origin element to
/// edit against, so that isolation isn't available there; an allow-list sanitizer (AngleSharp-
/// backed, not a regex strip) is the substitute defense. Applied both when content enters the
/// editable surface and again when it's read back, since a paste into a contenteditable region
/// can carry arbitrary HTML from the clipboard.
/// </summary>
public static class HtmlContentSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = new();

    public static string Sanitize(string html) => Sanitizer.Sanitize(html);
}
