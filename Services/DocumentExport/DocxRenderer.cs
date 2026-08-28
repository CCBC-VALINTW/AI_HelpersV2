using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Color = DocumentFormat.OpenXml.Wordprocessing.Color;

namespace AiHelpers.Services.DocumentExport;

/// <summary>
/// Renders the Block model (see HtmlBlockParser) into a .docx via DocumentFormat.OpenXml
/// (Microsoft's own Open XML SDK, MIT licensed - see the project's build report for why this was
/// chosen over anything requiring a paid/ambiguous-license library).
///
/// Deliberately skips a StyleDefinitionsPart (no "Heading1"/"ListParagraph" named styles) - heading
/// size/weight and list indentation are applied as direct run/paragraph formatting instead. That
/// sidesteps an entire class of "does Word actually recognise this style ID without an explicit
/// style part" risk for very little visible difference in the result; a real numbering part is
/// still required for lists to render as lists at all (ilvl/numId alone, with no named list style,
/// is exactly how Word itself represents a plain bulleted/numbered list).
/// </summary>
internal static class DocxRenderer
{
    private const int MaxListLevel = 4;
    private const int BulletNumId = 1;
    private const int DecimalNumId = 2;

    public static byte[] Render(string title, IReadOnlyList<Block> blocks)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            AddNumbering(mainPart);

            doc.PackageProperties.Title = title;

            foreach (var block in blocks)
            {
                AppendBlock(body, mainPart, block);
            }

            // A4 portrait, 2cm margins (matches this app's other exported-document conventions -
            // see BuildOutputDocument's own plain, unstyled default in HelperDetail.razor).
            body.AppendChild(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1134, Bottom = 1134, Left = 1134, Right = 1134 }));

            mainPart.Document.Save();
        }
        return stream.ToArray();
    }

    private static void AddNumbering(MainDocumentPart mainPart)
    {
        var numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
        var numbering = new Numbering();

        numbering.Append(BuildAbstractNum(BulletNumId, bullet: true));
        numbering.Append(BuildAbstractNum(DecimalNumId, bullet: false));
        numbering.Append(new NumberingInstance(new AbstractNumId { Val = BulletNumId }) { NumberID = BulletNumId });
        numbering.Append(new NumberingInstance(new AbstractNumId { Val = DecimalNumId }) { NumberID = DecimalNumId });

        numberingPart.Numbering = numbering;
        numberingPart.Numbering.Save();
    }

    private static AbstractNum BuildAbstractNum(int abstractNumId, bool bullet)
    {
        var abstractNum = new AbstractNum { AbstractNumberId = abstractNumId };
        for (var level = 0; level <= MaxListLevel; level++)
        {
            var indentTwips = (level + 1) * 720; // 720 twips = 0.5"
            var lvl = new Level
            {
                LevelIndex = level,
                StartNumberingValue = new StartNumberingValue { Val = 1 },
                NumberingFormat = new NumberingFormat { Val = bullet ? NumberFormatValues.Bullet : NumberFormatValues.Decimal },
                LevelText = new LevelText { Val = bullet ? BulletChar(level) : $"%{level + 1}." },
                LevelJustification = new LevelJustification { Val = LevelJustificationValues.Left },
                PreviousParagraphProperties = new PreviousParagraphProperties(
                    new Indentation { Left = indentTwips.ToString(), Hanging = "360" }),
            };
            if (bullet)
            {
                lvl.NumberingSymbolRunProperties = new NumberingSymbolRunProperties(
                    new RunFonts { Ascii = "Symbol", HighAnsi = "Symbol", Hint = FontTypeHintValues.Default });
            }
            abstractNum.Append(lvl);
        }
        return abstractNum;
    }

    // Plain round bullet at every level, kept deliberately simple (no open-circle/square variation
    // per depth) - Word's Symbol font glyph for a filled bullet is  regardless of level.
    private static string BulletChar(int level) => "";

    private static void AppendBlock(Body body, MainDocumentPart mainPart, Block block)
    {
        switch (block)
        {
            case PageBreakBlock:
                body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                break;
            case RuleBlock:
                body.AppendChild(BuildRuleParagraph());
                break;
            case ParagraphBlock p:
                body.AppendChild(BuildParagraph(mainPart, p));
                break;
            case TableBlock t:
                body.AppendChild(BuildTable(mainPart, t));
                break;
        }
    }

    private static Paragraph BuildRuleParagraph() => new(new ParagraphProperties(
        new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "999999" }),
        new SpacingBetweenLines { After = "160" }));

    private static Paragraph BuildParagraph(MainDocumentPart mainPart, ParagraphBlock p)
    {
        var pPr = new ParagraphProperties();

        if (p.Align is { } align)
        {
            pPr.Justification = new Justification { Val = ToJustification(align) };
        }

        if (p.ListKind != ListKind.None)
        {
            var level = Math.Clamp(p.ListLevel, 0, MaxListLevel);
            pPr.Indentation = new Indentation { Left = ((level + 1) * 720).ToString(), Hanging = "360" };
            pPr.NumberingProperties = new NumberingProperties(
                new NumberingLevelReference { Val = level },
                new NumberingId { Val = p.ListKind == ListKind.Bullet ? BulletNumId : DecimalNumId });
        }
        else
        {
            pPr.SpacingBetweenLines = new SpacingBetweenLines { After = "160" };
        }

        var (bold, size) = HeadingFormat(p.StyleName);
        if (size is not null)
        {
            pPr.SpacingBetweenLines = new SpacingBetweenLines { Before = "240", After = "120" };
        }

        var paragraph = new Paragraph { ParagraphProperties = pPr };
        foreach (var run in p.Runs)
        {
            paragraph.AppendChild(BuildRun(mainPart, run, bold, size));
        }
        return paragraph;
    }

    /// <summary>Direct heading formatting (see the class doc comment for why this skips named
    /// styles) - proportionally scaled sizes/weight, not an attempt to reproduce Word's exact
    /// built-in Heading1..6 theme values.</summary>
    private static (bool Bold, int? HalfPointSize) HeadingFormat(string styleName) => styleName switch
    {
        "Heading1" => (true, 64), // 32pt
        "Heading2" => (true, 52), // 26pt
        "Heading3" => (true, 44), // 22pt
        "Heading4" => (true, 36), // 18pt
        "Heading5" => (true, 32), // 16pt
        "Heading6" => (true, 28), // 14pt
        _ => (false, null),
    };

    private static OpenXmlElement BuildRun(MainDocumentPart mainPart, InlineRun run, bool headingBold, int? headingHalfPointSize)
    {
        if (run.IsLineBreak)
        {
            return new Run(BuildRunProperties(run, headingBold, headingHalfPointSize), new Break());
        }

        var textElement = new Text(run.Text) { Space = SpaceProcessingModeValues.Preserve };
        var innerRun = new Run(BuildRunProperties(run, headingBold, headingHalfPointSize), textElement);

        if (run.Href is null || !Uri.TryCreate(run.Href, UriKind.Absolute, out var uri)) return innerRun;

        var relationship = mainPart.AddHyperlinkRelationship(uri, true);
        return new Hyperlink(innerRun) { Id = relationship.Id, History = OnOffValue.FromBoolean(true) };
    }

    private static RunProperties BuildRunProperties(InlineRun run, bool headingBold, int? headingHalfPointSize)
    {
        var props = new RunProperties();
        if (run.Bold || headingBold) props.Bold = new Bold();
        if (run.Italic) props.Italic = new Italic();
        if (run.Underline) props.Underline = new Underline { Val = UnderlineValues.Single };
        if (run.Strike) props.Strike = new Strike();
        if (headingHalfPointSize is { } size) props.FontSize = new FontSize { Val = size.ToString() };
        if (run.Color is { } rgb) props.Color = new Color { Val = $"{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}" };
        else if (run.Href is not null) props.Color = new Color { Val = "0563C1" }; // Word's default hyperlink blue
        if (run.Href is not null) props.Underline ??= new Underline { Val = UnderlineValues.Single };
        return props;
    }

    private static JustificationValues ToJustification(BlockAlign align) => align switch
    {
        BlockAlign.Center => JustificationValues.Center,
        BlockAlign.Right => JustificationValues.Right,
        BlockAlign.Justify => JustificationValues.Both,
        _ => JustificationValues.Left,
    };

    private static Table BuildTable(MainDocumentPart mainPart, TableBlock t)
    {
        var table = new Table();

        // Child order matters - this is real OOXML schema, not just a convenient object
        // initializer order, confirmed the hard way via OpenXmlValidator while building this:
        // TableWidth must precede TableBorders within TableProperties, TableBorders' own edges
        // must appear Top/Left/Bottom/Right/InsideH/InsideV, and a TableGrid (one GridColumn per
        // column) must immediately follow TableProperties, before any row.
        table.AppendChild(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "999999" })));

        var columnCount = t.Rows.Count == 0 ? 1 : t.Rows.Max(r => r.Cells.Count);
        var grid = new TableGrid();
        for (var i = 0; i < columnCount; i++) grid.AppendChild(new GridColumn());
        table.AppendChild(grid);

        foreach (var row in t.Rows)
        {
            var tableRow = new TableRow();
            foreach (var cell in row.Cells)
            {
                var tableCell = new TableCell();
                if (cell.IsHeader)
                {
                    tableCell.AppendChild(new TableCellProperties(
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" }));
                }

                var cellBlocks = cell.Content.Count == 0
                    ? [new ParagraphBlock("Normal", [], null, 0, ListKind.None)]
                    : cell.Content;

                foreach (var contentBlock in cellBlocks)
                {
                    if (contentBlock is ParagraphBlock pb)
                    {
                        var paragraph = BuildParagraph(mainPart, cell.IsHeader ? pb with { StyleName = "Normal" } : pb);
                        if (cell.IsHeader)
                        {
                            foreach (var r in paragraph.Elements<Run>())
                            {
                                r.RunProperties ??= new RunProperties();
                                r.RunProperties.Bold = new Bold();
                            }
                        }
                        tableCell.AppendChild(paragraph);
                    }
                    else
                    {
                        // Nested tables/lists inside a cell aren't produced by this editor's own
                        // toolbar - skip rather than recurse indefinitely, matching the same "don't
                        // support what the toolbar can't create" scope as the rest of this parser.
                        tableCell.AppendChild(new Paragraph());
                    }
                }
                tableRow.AppendChild(tableCell);
            }
            table.AppendChild(tableRow);
        }
        return table;
    }
}
