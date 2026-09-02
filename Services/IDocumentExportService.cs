namespace AiHelpers.Services;

/// <summary>
/// Converts a GeneratedDocument's sanitized HtmlContent into a downloadable file for the document
/// editor's Word/HTML export buttons (DocumentFormat.OpenXml for docx, MIT licensed, no
/// revenue-based or per-seat terms - see the project's build report). See Services/DocumentExport/
/// for the actual rendering pipeline. PDF export doesn't go through this service at all - it's
/// triggered client-side against the live preview iframe via structuredEditor.js's printDocument(),
/// using the browser's own native print-to-PDF instead of a server-side reconstruction.
/// </summary>
public interface IDocumentExportService
{
    /// <summary>stylesheetCss only ever fills in a handful of tag-level defaults (heading colour,
    /// table border colour, base font) via DocumentExport/StylesheetTagStyles - DocxRenderer still
    /// has no real CSS selector/cascade engine, so this is best-effort, not full fidelity with the
    /// live preview the way HTML export now is.</summary>
    byte[] ToDocx(string title, string html, string? stylesheetCss);
    byte[] ToHtml(string title, string html, string? stylesheetCss);
}
