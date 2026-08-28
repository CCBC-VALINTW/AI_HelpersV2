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
    // Bare defaults strip class/id from every element and remove <style> tags entirely (content
    // included, not just the tag) - fine for arbitrary untrusted HTML in general, but this app's
    // documents routinely depend on both: the Stylesheet system's CSS targets ".rendDoc"-scoped
    // classes, and a model run under "No Stylesheet" embeds its own <style> block directly in its
    // output. Without these, any styled document goes fully unstyled the moment it's sanitized.
    // Ganss.Xss still sanitizes <style> content and these attributes the same way it does the
    // style="" attribute (dangerous CSS - -moz-binding, javascript: URLs, etc. - is stripped, not
    // the whole tag) - confirmed via a standalone probe that on*/javascript: vectors are still
    // neutralized with these allowed, so this doesn't reopen a real XSS path.
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("id");
        sanitizer.AllowedTags.Add("style");
        return sanitizer;
    }

    public static string Sanitize(string html) => Sanitizer.Sanitize(html);
}
