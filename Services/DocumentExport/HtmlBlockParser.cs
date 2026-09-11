using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace AiHelpers.Services.DocumentExport;

/// <summary>
/// Parses a GeneratedDocument's sanitized HTML (TinyMCE output, already passed through
/// HtmlContentSanitizer on save) into the Block model DocxRenderer/PdfRenderer walk to build the
/// actual exported files. Deliberately narrow rather than a general HTML-to-anything converter -
/// only handles what the document editor's own toolbar/plugin set can produce (see
/// DocumentEditor.razor's createEditor call): headings, paragraphs, bold/italic/underline/
/// strikethrough, coloured text, left/center/right/justify alignment, bullet/numbered lists,
/// tables, links, line breaks, horizontal rules, and the page-break marker.
///
/// The pagebreak plugin's own default output is the HTML comment `&lt;!-- pagebreak --&gt;` - that
/// does NOT survive HtmlContentSanitizer, which strips comments by default (confirmed empirically
/// while building this, not assumed), so DocumentEditor.razor configures pagebreak_separator to
/// emit a real styled div instead (page-break-before:always, which does survive - the sanitizer's
/// default allowed CSS properties include it) - see IsPageBreak below.
///
/// Anything else (an unrecognised tag, e.g. from a paste TinyMCE didn't fully normalise) is walked
/// as a plain block/inline container rather than dropped, so content is never silently lost - just
/// unstyled.
/// </summary>
internal static partial class HtmlBlockParser
{
    public static List<Block> Parse(string bodyHtml)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument($"<!DOCTYPE html><html><body>{bodyHtml}</body></html>");
        return WalkBlocks(document.Body!, listLevel: 0, listKind: ListKind.None);
    }

    private static readonly HashSet<string> BlockTags =
        ["p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "table", "blockquote", "hr"];

    private static List<Block> WalkBlocks(INode container, int listLevel, ListKind listKind)
    {
        var blocks = new List<Block>();
        foreach (var child in container.ChildNodes)
        {
            AppendBlock(blocks, child, listLevel, listKind);
        }
        return blocks;
    }

    private static void AppendBlock(List<Block> blocks, INode node, int listLevel, ListKind listKind)
    {
        if (node.NodeType == NodeType.Text)
        {
            var text = node.TextContent;
            if (!string.IsNullOrWhiteSpace(text))
            {
                blocks.Add(new ParagraphBlock("Normal", [new InlineRun(text, false, false, false, false, null, null, false)], null, listLevel, listKind));
            }
            return;
        }

        if (node is not IElement el) return;

        switch (el.TagName.ToLowerInvariant())
        {
            case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                blocks.Add(new ParagraphBlock($"Heading{el.TagName[1]}", ExtractRuns(el), ReadAlign(el), 0, ListKind.None));
                return;

            case "p":
                if (IsPageBreak(el)) { blocks.Add(new PageBreakBlock()); return; }
                var runs = ExtractRuns(el);
                if (runs.Count > 0) blocks.Add(new ParagraphBlock("Normal", runs, ReadAlign(el), listLevel, listKind));
                return;

            case "div":
                if (IsPageBreak(el)) { blocks.Add(new PageBreakBlock()); return; }
                // A generic container (TinyMCE sometimes wraps content in divs) - unwrap rather
                // than skip, so nested block content still comes through.
                blocks.AddRange(WalkBlocks(el, listLevel, listKind));
                return;

            case "blockquote":
                blocks.AddRange(WalkBlocks(el, listLevel, listKind));
                return;

            case "hr":
                blocks.Add(new RuleBlock());
                return;

            case "ul":
                AppendList(blocks, el, listLevel, ListKind.Bullet);
                return;

            case "ol":
                AppendList(blocks, el, listLevel, ListKind.Number);
                return;

            case "table":
                blocks.Add(BuildTable(el));
                return;

            case "br":
                return; // handled inline within ExtractRuns, not reached as a direct block child in practice

            case "img":
                if (ReadDataUriImage(el) is { } image) blocks.Add(image);
                return;

            case "svg":
                // Reached only when the browser-side rasterisation didn't run or couldn't convert
                // this one (see outputActions.js's rasterizeSvgsForClipboard, which leaves a failed
                // graphic's original SVG in place). Dropped rather than allowed to fall through to
                // the default branch, which would scrape the chart's <text> labels out as a run of
                // loose paragraphs - axis numbers and legend words stranded in the document with no
                // chart around them, which reads worse than the graphic simply being absent.
                return;

            case "style" or "script" or "head" or "title" or "meta" or "link":
                // Never exportable content - without this, the default branch below extracts a
                // <style>/<script> element's raw text (CSS/JS source) as if it were a plain
                // paragraph, since neither has element children for HasBlockDescendant to find.
                // head/title/meta/link are defensive - bodyHtml is a fragment in practice, but
                // costs nothing to exclude them too if one ever shows up.
                return;

            default:
                // Inline-only or unknown wrapper sitting at block position (a bare span/a/strong,
                // or an unrecognised tag) - recurse if it has block children, otherwise treat its
                // own text as one paragraph.
                if (HasBlockDescendant(el))
                {
                    blocks.AddRange(WalkBlocks(el, listLevel, listKind));
                }
                else
                {
                    var inlineRuns = ExtractRuns(el);
                    if (inlineRuns.Count > 0) blocks.Add(new ParagraphBlock("Normal", inlineRuns, null, listLevel, listKind));
                }
                return;
        }
    }

    /// <summary>
    /// Each &lt;li&gt;'s own direct inline content becomes one paragraph at the current list
    /// level/kind; a nested &lt;ul&gt;/&lt;ol&gt; inside it recurses at listLevel + 1, exactly
    /// mirroring how nested lists actually render (an indented sub-list under the item it's
    /// nested in, not a sibling of it).
    /// </summary>
    private static void AppendList(List<Block> blocks, IElement listEl, int listLevel, ListKind kind)
    {
        foreach (var li in listEl.Children)
        {
            if (!li.TagName.Equals("li", StringComparison.OrdinalIgnoreCase)) continue;

            var directRuns = ExtractRuns(li, stopAtNestedLists: true);
            if (directRuns.Count > 0)
            {
                blocks.Add(new ParagraphBlock("Normal", directRuns, null, listLevel, kind));
            }

            foreach (var nested in li.Children)
            {
                if (nested.TagName.Equals("ul", StringComparison.OrdinalIgnoreCase))
                    AppendList(blocks, nested, listLevel + 1, ListKind.Bullet);
                else if (nested.TagName.Equals("ol", StringComparison.OrdinalIgnoreCase))
                    AppendList(blocks, nested, listLevel + 1, ListKind.Number);
            }
        }
    }

    private static TableBlock BuildTable(IElement tableEl)
    {
        var rows = new List<TableRowBlock>();
        foreach (var rowEl in tableEl.QuerySelectorAll("tr"))
        {
            var cells = new List<TableCellBlock>();
            foreach (var cellEl in rowEl.Children)
            {
                var tag = cellEl.TagName.ToLowerInvariant();
                if (tag != "td" && tag != "th") continue;
                cells.Add(new TableCellBlock(WalkBlocks(cellEl, 0, ListKind.None), tag == "th"));
            }
            if (cells.Count > 0) rows.Add(new TableRowBlock(cells));
        }
        return new TableBlock(rows);
    }

    private static bool HasBlockDescendant(IElement el) =>
        el.Children.Any(c => BlockTags.Contains(c.TagName.ToLowerInvariant()) || HasBlockDescendant(c));

    private static bool IsPageBreak(IElement el) =>
        PageBreakStylePattern().IsMatch(el.GetAttribute("style") ?? "");

    private static BlockAlign? ReadAlign(IElement el)
    {
        var match = TextAlignPattern().Match(el.GetAttribute("style") ?? "");
        if (!match.Success) return null;
        return match.Groups[1].Value.ToLowerInvariant() switch
        {
            "center" => BlockAlign.Center,
            "right" => BlockAlign.Right,
            "justify" => BlockAlign.Justify,
            _ => BlockAlign.Left,
        };
    }

    /// <summary>
    /// Walks an element's inline descendants (text, strong/b, em/i, u, s/strike/del, span/a with
    /// colour, br) into a flat run list, carrying formatting down from ancestors so e.g.
    /// "&lt;strong&gt;bold &lt;em&gt;and italic&lt;/em&gt;&lt;/strong&gt;" produces two runs, the
    /// second with both flags set. stopAtNestedLists is used by AppendList so a &lt;li&gt;'s own
    /// paragraph text doesn't also swallow a nested ul/ol's text (that's walked separately, at the
    /// next indent level).
    /// </summary>
    private static List<InlineRun> ExtractRuns(INode root, bool stopAtNestedLists = false)
    {
        var runs = new List<InlineRun>();
        Walk(root, bold: false, italic: false, underline: false, strike: false, color: null, href: null);
        return runs;

        void Walk(INode node, bool bold, bool italic, bool underline, bool strike, (byte, byte, byte)? color, string? href)
        {
            foreach (var child in node.ChildNodes)
            {
                if (child.NodeType == NodeType.Text)
                {
                    var text = child.TextContent;
                    if (text.Length > 0)
                    {
                        runs.Add(new InlineRun(text, bold, italic, underline, strike, color, href, false));
                    }
                    continue;
                }

                if (child is not IElement el) continue;
                var tag = el.TagName.ToLowerInvariant();

                if (stopAtNestedLists && (tag == "ul" || tag == "ol")) continue;

                switch (tag)
                {
                    case "br":
                        runs.Add(new InlineRun("", bold, italic, underline, strike, color, href, true));
                        continue;
                    case "strong" or "b":
                        Walk(el, true, italic, underline, strike, color, href);
                        continue;
                    case "em" or "i":
                        Walk(el, bold, true, underline, strike, color, href);
                        continue;
                    case "u":
                        Walk(el, bold, italic, true, strike, color, href);
                        continue;
                    case "s" or "strike" or "del":
                        Walk(el, bold, italic, underline, true, color, href);
                        continue;
                    case "a":
                        var linkHref = el.GetAttribute("href");
                        Walk(el, bold, italic, underline, strike, color, string.IsNullOrWhiteSpace(linkHref) ? href : linkHref);
                        continue;
                    default:
                        Walk(el, bold, italic, underline, strike, ReadColor(el) ?? color, href);
                        continue;
                }
            }
        }
    }

    /// <summary>
    /// Reads an &lt;img&gt; whose src is a base64 data URI into an ImageBlock.
    /// <para>
    /// A remote (http/https/file) src is deliberately NOT fetched. Doing so would mean this export
    /// endpoint issuing arbitrary outbound requests on a caller's behalf against a URL embedded in
    /// model-generated content - the SSRF surface that IUrlFetchService exists to guard for the
    /// URL-as-input feature, and the reason docx image support was originally left out rather than
    /// half-built. Data URIs need none of that: the bytes are already inline, no request happens,
    /// and they're the only kind this app actually produces (real output data has no remote images
    /// at all). An image that isn't a data URI is skipped rather than fetched.
    /// </para>
    /// </summary>
    private static ImageBlock? ReadDataUriImage(IElement el)
    {
        var match = DataUriPattern().Match(el.GetAttribute("src") ?? "");
        if (!match.Success) return null;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(match.Groups["data"].Value);
        }
        catch (FormatException)
        {
            return null; // Truncated or otherwise malformed base64 - skip the image, keep the document.
        }
        if (bytes.Length == 0) return null;

        // Width/height come from the attributes the rasteriser sets. Falling back to a sensible box
        // rather than guessing at the real pixel dimensions (which would mean decoding the image
        // here) - an unsized image is not something this app's own pipeline produces.
        var width = ReadPixelAttribute(el, "width") ?? 600;
        var height = ReadPixelAttribute(el, "height") ?? 400;

        var alt = el.GetAttribute("alt");
        return new ImageBlock(bytes, $"image/{match.Groups["subtype"].Value.ToLowerInvariant()}", width, height,
            string.IsNullOrWhiteSpace(alt) ? null : alt);
    }

    private static int? ReadPixelAttribute(IElement el, string name) =>
        int.TryParse(el.GetAttribute(name), out var value) && value > 0 ? value : null;

    /// <summary>Only the raster formats Word can embed directly - an SVG data URI deliberately
    /// doesn't match, since embedding one would need a raster fallback generated alongside it.</summary>
    [GeneratedRegex(@"^data:image/(?<subtype>png|jpeg|jpg|gif|bmp);base64,(?<data>[A-Za-z0-9+/=\s]+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex DataUriPattern();

    private static (byte, byte, byte)? ReadColor(IElement el)
    {
        var match = ColorStylePattern().Match(el.GetAttribute("style") ?? "");
        return match.Success ? ColorParsing.ToRgb(match.Groups[1].Value.Trim()) : null;
    }

    [GeneratedRegex(@"page-break-before\s*:\s*always", RegexOptions.IgnoreCase)]
    private static partial Regex PageBreakStylePattern();

    [GeneratedRegex(@"text-align\s*:\s*(left|center|right|justify)", RegexOptions.IgnoreCase)]
    private static partial Regex TextAlignPattern();

    // Negative lookbehind excludes "background-color:" - both can be present on the same style
    // attribute (TinyMCE's forecolor and backcolor toolbar buttons write one each).
    [GeneratedRegex(@"(?<![a-z-])color\s*:\s*([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ColorStylePattern();
}
