namespace AiHelpers.Services;

/// <summary>
/// Fetches a web page (or other document) as an attachment-ready blob, server-side, so Helper
/// input can include "content from this URL" alongside typed text and uploaded files. Guards
/// against SSRF (see UrlFetchService) - callers should surface ErrorMessage to the user rather
/// than assume Success.
/// </summary>
public interface IUrlFetchService
{
    Task<UrlFetchResult> FetchAsync(string url, long maxBytes, CancellationToken cancellationToken = default);
}

public class UrlFetchResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? Bytes { get; set; }

    /// <summary>Matches AttachmentClassifier's format vocabulary (html/pdf/txt/md/csv).</summary>
    public string? Format { get; set; }

    /// <summary>The URL actually fetched, after following any (validated) redirects.</summary>
    public string? FinalUrl { get; set; }
}
