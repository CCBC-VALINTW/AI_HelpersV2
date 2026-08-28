namespace AiHelpers.Services.DocumentExport;

/// <summary>
/// The small structural model HtmlBlockParser produces from a GeneratedDocument's sanitized HTML,
/// and that DocxRenderer/PdfRenderer both walk to build the actual exported files - see
/// HtmlBlockParser's own doc comment for what HTML this is intentionally narrow to.
/// </summary>
internal enum ListKind { None, Bullet, Number }

internal enum BlockAlign { Left, Center, Right, Justify }

/// <summary>One run of text within a paragraph/heading/table cell, carrying whatever inline
/// formatting applies to it. IsLineBreak marks a forced &lt;br&gt; - Text is empty in that case.</summary>
internal sealed record InlineRun(
    string Text,
    bool Bold,
    bool Italic,
    bool Underline,
    bool Strike,
    (byte R, byte G, byte B)? Color,
    string? Href,
    bool IsLineBreak);

internal abstract record Block;

/// <summary>StyleName is "Normal" or "Heading1".."Heading6". ListKind/ListLevel are None/0 for a
/// plain paragraph; a bulleted/numbered paragraph carries its nesting depth (0-based) here rather
/// than as a separate list container block, since TinyMCE list items are exactly one paragraph of
/// content each (see HtmlBlockParser.AppendList).</summary>
internal sealed record ParagraphBlock(
    string StyleName,
    IReadOnlyList<InlineRun> Runs,
    BlockAlign? Align,
    int ListLevel,
    ListKind ListKind) : Block;

// Named *Block, not TableRow/TableCell, to avoid colliding with DocumentFormat.OpenXml.
// Wordprocessing's own same-named types - DocxRenderer.cs imports that namespace, and a
// same-namespace type (this one) would otherwise win name resolution over a `using`-imported one,
// silently binding to the wrong type.
internal sealed record TableCellBlock(IReadOnlyList<Block> Content, bool IsHeader);

internal sealed record TableRowBlock(IReadOnlyList<TableCellBlock> Cells);

internal sealed record TableBlock(IReadOnlyList<TableRowBlock> Rows) : Block;

internal sealed record RuleBlock : Block;

/// <summary>A user-inserted page break (the pagebreak plugin's toolbar button) - see
/// DocumentEditor.razor's pagebreak_separator config for why this is recognised from a styled div
/// rather than the plugin's own default HTML-comment marker.</summary>
internal sealed record PageBreakBlock : Block;
