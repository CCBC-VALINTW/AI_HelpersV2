using System.Text;
using AiHelpers.Services.DocumentExport;

namespace AiHelpers.Services;

public class DocumentExportService : IDocumentExportService
{
    public byte[] ToDocx(string title, string html, string? stylesheetCss) =>
        DocxRenderer.Render(title, HtmlBlockParser.Parse(html), StylesheetTagStyles.Extract(stylesheetCss));

    /// <summary>The plain HTML export needs no parsing/rendering pipeline - HtmlContent is already
    /// exactly this document's HTML; this just wraps it as a standalone, self-contained document,
    /// reusing the exact same composition (rendDoc wrap + stylesheet CSS) as the live preview, via
    /// OutputDocumentBuilder - see HelperDetail.razor's own BuildOutputDocument/download-as-HTML
    /// feature, which this now matches instead of ignoring the stylesheet entirely.</summary>
    public byte[] ToHtml(string title, string html, string? stylesheetCss) =>
        Encoding.UTF8.GetBytes(OutputDocumentBuilder.Build(html, stylesheetCss, title));
}
