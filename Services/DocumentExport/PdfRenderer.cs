using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace AiHelpers.Services.DocumentExport;

/// <summary>
/// Renders the Block model (see HtmlBlockParser) into a PDF via PDFsharp/MigraDoc (MIT licensed,
/// no revenue threshold or "community edition" terms - see the project's build report for why this
/// was chosen over QuestPDF, which does have one). Not a pixel-perfect HTML-to-PDF renderer (no
/// headless browser involved) - a semantic mapping of the same Block model DocxRenderer uses, so
/// what exports to Word and what exports to PDF are always structurally consistent with each
/// other, even if MigraDoc's own layout engine doesn't reproduce TinyMCE's on-screen appearance
/// exactly.
///
/// Bullet/numbered lists use manually-written bullet characters / numbers rather than MigraDoc's
/// own ListInfo/ListType mechanism - simpler and more predictable to get right than relying on its
/// auto-restart semantics across a flat, already-parsed block sequence (numbering state is tracked
/// here as blocks are walked in document order, since the parser has already flattened each list
/// item down to one ParagraphBlock carrying its level/kind - see HtmlBlockParser.AppendList).
/// </summary>
internal static class PdfRenderer
{
    private const int MaxListLevel = 4;

    public static byte[] Render(string title, IReadOnlyList<Block> blocks)
    {
        var document = new Document();
        document.Info.Title = title;

        // "Arial" rather than this app's usual Calibri (see BuildOutputDocument in
        // HelperDetail.razor) - PDFsharp 6's Core build has no font strategy of its own under a
        // portable TFM; GlobalFontSettings.UseWindowsFontsUnderWindows (set once in
        // DocumentExportService's static constructor) resolves "some standard fonts like Arial or
        // Times New Roman" per its own doc comment, and Arial is guaranteed present on every
        // Windows Server release this app is deployed to, unlike Calibri on older/Core builds.
        document.Styles.Normal.Font.Name = "Arial";
        document.Styles.Normal.Font.Size = 11;

        var section = document.AddSection();

        // Document.DefaultPageSetup is a read-only template (confirmed at runtime, not just from
        // docs - it throws InvalidOperationException if written to directly); each Section gets
        // its own PageSetup, pre-populated as a mutable clone of DefaultPageSetup, and that's the
        // one page setup is actually configured on.
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);

        var listCounters = new Dictionary<int, int>();
        ListKind? previousListKind = null;
        var previousListLevel = -1;

        foreach (var block in blocks)
        {
            if (block is ParagraphBlock { ListKind: ListKind.Number } numbered)
            {
                var level = Math.Clamp(numbered.ListLevel, 0, MaxListLevel);
                if (previousListKind != ListKind.Number || previousListLevel != level)
                {
                    foreach (var key in listCounters.Keys.Where(k => k >= level).ToList())
                    {
                        listCounters.Remove(key);
                    }
                    listCounters[level] = 1;
                }
                else
                {
                    listCounters[level] = listCounters.GetValueOrDefault(level, 0) + 1;
                }
            }

            AppendBlock(section, block, listCounters);

            if (block is ParagraphBlock p)
            {
                previousListKind = p.ListKind == ListKind.None ? null : p.ListKind;
                previousListLevel = Math.Clamp(p.ListLevel, 0, MaxListLevel);
            }
            else
            {
                previousListKind = null;
                previousListLevel = -1;
            }
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private static void AppendBlock(Section section, Block block, Dictionary<int, int> listCounters)
    {
        switch (block)
        {
            case PageBreakBlock:
                section.AddPageBreak();
                break;
            case RuleBlock:
                var rule = section.AddParagraph();
                rule.Format.Borders.Bottom = new Border { Width = Unit.FromPoint(0.75), Color = new Color(153, 153, 153) };
                rule.Format.SpaceAfter = Unit.FromPoint(10);
                break;
            case ParagraphBlock p:
                AppendParagraph(section.AddParagraph(), p, listCounters);
                break;
            case TableBlock t:
                AppendTable(section.AddTable(), t);
                break;
        }
    }

    private static void AppendParagraph(Paragraph paragraph, ParagraphBlock p, Dictionary<int, int> listCounters)
    {
        var (bold, size) = HeadingFormat(p.StyleName);
        if (size is { } pt)
        {
            paragraph.Format.Font.Bold = bold;
            paragraph.Format.Font.Size = Unit.FromPoint(pt);
            paragraph.Format.SpaceBefore = Unit.FromPoint(14);
            paragraph.Format.SpaceAfter = Unit.FromPoint(6);
        }
        else
        {
            paragraph.Format.SpaceAfter = Unit.FromPoint(8);
        }

        if (p.Align is { } align)
        {
            paragraph.Format.Alignment = align switch
            {
                BlockAlign.Center => ParagraphAlignment.Center,
                BlockAlign.Right => ParagraphAlignment.Right,
                BlockAlign.Justify => ParagraphAlignment.Justify,
                _ => ParagraphAlignment.Left,
            };
        }

        if (p.ListKind != ListKind.None)
        {
            var level = Math.Clamp(p.ListLevel, 0, MaxListLevel);
            paragraph.Format.LeftIndent = Unit.FromCentimeter(0.6 * (level + 1));
            paragraph.Format.FirstLineIndent = Unit.FromCentimeter(-0.6);

            var marker = p.ListKind == ListKind.Bullet
                ? BulletChar(level) + "\t"
                : $"{listCounters.GetValueOrDefault(level, 1)}.\t";
            paragraph.AddFormattedText(marker);
        }

        AppendRuns(paragraph, p.Runs, bold);
    }

    private static void AppendRuns(Paragraph paragraph, IReadOnlyList<InlineRun> runs, bool forceBold)
    {
        foreach (var run in runs)
        {
            if (run.IsLineBreak)
            {
                paragraph.AddLineBreak();
                continue;
            }
            if (string.IsNullOrEmpty(run.Text)) continue;

            if (run.Href is not null)
            {
                var hyperlink = paragraph.AddHyperlink(run.Href, HyperlinkType.Web);
                var linked = hyperlink.AddFormattedText(run.Text);
                ApplyRunFormat(linked, run, forceBold);
                linked.Color = run.Color is { } rgb ? new Color(rgb.R, rgb.G, rgb.B) : new Color(5, 99, 193);
                linked.Underline = Underline.Single;
                continue;
            }

            var formatted = paragraph.AddFormattedText(run.Text);
            ApplyRunFormat(formatted, run, forceBold);
        }
    }

    private static void ApplyRunFormat(FormattedText formatted, InlineRun run, bool forceBold)
    {
        if (run.Bold || forceBold) formatted.Bold = true;
        if (run.Italic) formatted.Italic = true;
        if (run.Underline) formatted.Underline = Underline.Single;
        // No native strikethrough on MigraDoc's Font/FormattedText model - degrades to plain text
        // rather than failing; docx export (which does support it via OpenXML Strike) is the more
        // faithful of the two for this one attribute.
        if (run.Color is { } rgb) formatted.Color = new Color(rgb.R, rgb.G, rgb.B);
    }

    private static (bool Bold, double? PointSize) HeadingFormat(string styleName) => styleName switch
    {
        "Heading1" => (true, 24d),
        "Heading2" => (true, 20d),
        "Heading3" => (true, 17d),
        "Heading4" => (true, 14d),
        "Heading5" => (true, 12.5d),
        "Heading6" => (true, 11.5d),
        _ => (false, null),
    };

    private static string BulletChar(int level) => level % 2 == 0 ? "•" : "◦";

    private static void AppendTable(Table table, TableBlock t)
    {
        table.Borders.Width = Unit.FromPoint(0.5);
        table.Borders.Color = new Color(153, 153, 153);
        table.Format.SpaceAfter = Unit.FromPoint(10);

        var columnCount = t.Rows.Count == 0 ? 1 : t.Rows.Max(r => r.Cells.Count);
        var columnWidth = Unit.FromCentimeter(17.0 / columnCount);
        for (var i = 0; i < columnCount; i++) table.AddColumn(columnWidth);

        foreach (var row in t.Rows)
        {
            var migraRow = table.AddRow();
            var isHeaderRow = row.Cells.Count > 0 && row.Cells.All(c => c.IsHeader);
            if (isHeaderRow) migraRow.Shading.Color = new Color(242, 242, 242);

            for (var i = 0; i < row.Cells.Count; i++)
            {
                var cell = migraRow.Cells[i];
                var cellContent = row.Cells[i].Content;
                var cellBlocks = cellContent.Count == 0
                    ? [new ParagraphBlock("Normal", [], null, 0, ListKind.None)]
                    : cellContent;

                foreach (var contentBlock in cellBlocks)
                {
                    if (contentBlock is not ParagraphBlock pb) continue;
                    var paragraph = cell.AddParagraph();
                    AppendParagraph(paragraph, pb with { ListKind = ListKind.None, ListLevel = 0 }, listCounters: []);
                    if (row.Cells[i].IsHeader) paragraph.Format.Font.Bold = true;
                }
            }
        }
    }
}
