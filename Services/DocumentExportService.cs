using System.Net;
using System.Text;
using AiHelpers.Services.DocumentExport;

namespace AiHelpers.Services;

public class DocumentExportService : IDocumentExportService
{
    public byte[] ToDocx(string title, string html) => DocxRenderer.Render(title, HtmlBlockParser.Parse(html));

    /// <summary>The plain HTML export needs no parsing/rendering pipeline - HtmlContent is already
    /// exactly this document's HTML, sanitized on every save (see GeneratedDocument's doc
    /// comment); this just wraps it as a standalone, self-contained document, the same shape as
    /// HelperDetail.razor's own BuildOutputDocument/download-as-HTML feature.</summary>
    public byte[] ToHtml(string title, string html) => Encoding.UTF8.GetBytes($$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8" />
        <title>{{WebUtility.HtmlEncode(title)}}</title>
        <style>body { font-family: Calibri, Arial, sans-serif; margin: 1rem; }</style>
        </head>
        <body>{{html}}</body>
        </html>
        """);
}
