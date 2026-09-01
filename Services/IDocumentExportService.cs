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
    byte[] ToDocx(string title, string html);
    byte[] ToHtml(string title, string html);
}
