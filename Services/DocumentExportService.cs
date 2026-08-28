using System.Net;
using System.Text;
using AiHelpers.Services.DocumentExport;
using PdfSharp.Fonts;

namespace AiHelpers.Services;

public class DocumentExportService : IDocumentExportService
{
    static DocumentExportService()
    {
        // Must be set exactly once, before any font operation is performed, application-wide -
        // see docs.pdfsharp.net/PDFsharp/Topics/Fonts/Font-Resolving.html. PDFsharp 6's Core build
        // (the cross-platform, non-GDI one that comes from the plain "PDFsharp" NuGet package) has
        // no font-resolving strategy of its own under a portable TargetFramework and throws unless
        // a resolver is configured. This app is only ever deployed to Windows Server (see
        // Program.cs's DPAPI comment) - enabling this flag lets PDFsharp use its own built-in
        // WindowsPlatformFontResolver to resolve standard fonts (Arial, Times New Roman) directly,
        // rather than this app needing to write and maintain a custom IFontResolver.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    public byte[] ToDocx(string title, string html) => DocxRenderer.Render(title, HtmlBlockParser.Parse(html));

    public byte[] ToPdf(string title, string html) => PdfRenderer.Render(title, HtmlBlockParser.Parse(html));

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
