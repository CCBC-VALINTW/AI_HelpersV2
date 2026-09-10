using System.Net;
using System.Text.RegularExpressions;

namespace AiHelpers.Services;

/// <summary>
/// The plain-text half of a rich clipboard copy (see wwwroot/js/outputActions.js's
/// copyToClipboard) - what a paste into Notepad, a plain-text email or a form field receives,
/// while the HTML half carries the real formatting.
///
/// Deliberately derived server-side, which is NOT where V1 did it - its GovService form set
/// innerHTML on a detached div and read innerText back. Two things stop that porting cleanly:
/// innerText only does layout-aware line breaking for an element that's actually rendered, so on a
/// detached div it silently degrades to textContent (every block running together, and any style
/// block's CSS included as visible text) - and getting real innerText behaviour would mean
/// injecting model-generated HTML into this app's own live DOM, precisely what the sandboxed output
/// iframes exist to prevent. A string transform here gets sensible line breaks with neither
/// problem.
/// </summary>
public static class HtmlToPlainText
{
    public static string Convert(string html)
    {
        // Dropped wholesale rather than tag-stripped - their CONTENT is CSS/JS source, which would
        // otherwise land in the paste as visible text. OutputDocumentBuilder always embeds a style
        // block, so that's the normal case here, not an edge one.
        var text = Regex.Replace(html, "<(style|script)[^>]*>.*?</\\1>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        text = Regex.Replace(text, "<br[^>]*>", "\n", RegexOptions.IgnoreCase);
        // Cell boundaries become tabs before the block pass below turns </tr> into a newline, so a
        // table arrives as recognisable rows and columns rather than one run of words.
        text = Regex.Replace(text, "</(td|th)>", "\t", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</(p|div|h[1-6]|li|ul|ol|tr|table|section|article|header|footer|blockquote|pre)>", "\n", RegexOptions.IgnoreCase);

        text = Regex.Replace(text, "<[^>]*>", "");
        text = WebUtility.HtmlDecode(text);

        // Nested blocks each contribute their own newline, so collapse the resulting runs - and
        // drop the trailing whitespace a "</td>\t</tr>\n" sequence leaves at each line's end.
        text = Regex.Replace(text, @"[ \t]+(\r?\n)", "$1");
        text = Regex.Replace(text, @"(\r?\n){3,}", "\n\n");
        return text.Trim();
    }
}
