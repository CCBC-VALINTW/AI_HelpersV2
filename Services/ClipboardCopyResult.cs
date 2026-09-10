namespace AiHelpers.Services;

/// <summary>
/// What wwwroot/js/outputActions.js's copyToClipboard actually managed to put on the clipboard -
/// see that function for why the flavour is worth reporting (a silent downgrade to plain text loses
/// every bit of formatting the button exists for), and rasterizeSvgsForClipboard for what the SVG
/// counts mean.
/// </summary>
/// <param name="Flavour">"rich" (HTML + plain text), "plain" (plain text only), or "failed".</param>
public sealed record ClipboardCopyResult(string Flavour, int SvgsConverted, int SvgsFailed)
{
    /// <summary>Shared so the run page and the document editor can't drift into describing the same
    /// outcome differently.</summary>
    public string Describe() => Flavour switch
    {
        "rich" => "Copied - paste into Word or Outlook to keep the formatting." + DescribeGraphics(),
        "plain" => "Copied as plain text only - this browser couldn't put formatted content on the clipboard.",
        _ => "Couldn't copy to the clipboard - your browser blocked it."
    };

    private string DescribeGraphics()
    {
        // Said out loud rather than silently: a chart left as raw SVG is the one part of the document
        // that won't survive the paste, so "it copied fine" on its own would be misleading.
        if (SvgsFailed > 0)
        {
            return $" {SvgsConverted} of {SvgsConverted + SvgsFailed} graphics became images - the rest were left as they were and may not appear when pasted.";
        }

        return SvgsConverted switch
        {
            0 => "",
            1 => " Its graphic was converted to an image so it survives the paste.",
            _ => $" Its {SvgsConverted} graphics were converted to images so they survive the paste."
        };
    }
}
