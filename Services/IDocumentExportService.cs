namespace AiHelpers.Services;

/// <summary>
/// Converts a GeneratedDocument's sanitized HtmlContent into a downloadable file for the document
/// editor's three export buttons. See Services/DocumentExport/ for the actual rendering pipeline
/// and the project's build report for why each library was chosen (DocumentFormat.OpenXml for
/// docx, PDFsharp/MigraDoc for pdf - both MIT licensed, no revenue-based or per-seat terms).
/// </summary>
public interface IDocumentExportService
{
    byte[] ToDocx(string title, string html);
    byte[] ToPdf(string title, string html);
    byte[] ToHtml(string title, string html);
}
