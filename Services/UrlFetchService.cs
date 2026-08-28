using System.Net;

namespace AiHelpers.Services;

/// <summary>
/// Fetches a URL server-side for use as Helper input. This makes outbound requests on behalf of
/// whoever's signed in, to whatever host they type - a textbook SSRF surface for an internal
/// council app, so every hop (including redirects) is validated before being followed:
///   - scheme must be http/https
///   - the host must either match a configured internal allowlist (UrlFetch:AllowedInternalHostSuffixes
///     in appsettings.json, e.g. intranet.corp.conwy.gov.uk) OR resolve to only public IP addresses -
///     loopback/private/link-local ranges (including the 169.254.169.254 cloud metadata address) are
///     rejected unless the host is allowlisted
///   - redirects are followed manually (AllowAutoRedirect is off on the registered HttpClient) so
///     each hop gets the same validation - otherwise a public URL could 302 straight into a blocked
///     range and bypass the check entirely
///   - response size is capped by the caller-supplied maxBytes, same limit HelperDetail.razor
///     already applies to uploaded documents
/// Known simplification: the allowlist/IP check and the actual HTTP connection are two separate
/// steps, so a host that changes its DNS answer between the two (DNS rebinding) isn't fully closed
/// off. Accepted for this threat model (authenticated internal council staff, not an adversarial
/// public-internet input) - would need a custom connect callback pinning the validated IP to close
/// completely.
/// </summary>
public class UrlFetchService(HttpClient httpClient, IConfiguration configuration, ILogger<UrlFetchService> logger) : IUrlFetchService
{
    private const int MaxRedirects = 5;

    private static readonly IPNetwork[] BlockedRanges =
    [
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"), // link-local, includes the 169.254.169.254 cloud metadata address
        IPNetwork.Parse("::1/128"),
        IPNetwork.Parse("fc00::/7"),       // unique local (IPv6 equivalent of RFC1918)
        IPNetwork.Parse("fe80::/10"),      // link-local (IPv6)
    ];

    private readonly string[] _allowedInternalHostSuffixes =
        configuration.GetSection("UrlFetch:AllowedInternalHostSuffixes").Get<string[]>() ?? [];

    public async Task<UrlFetchResult> FetchAsync(string url, long maxBytes, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return Fail("That doesn't look like a valid URL.");
        }

        for (var hop = 0; hop < MaxRedirects; hop++)
        {
            var (allowed, error) = await ValidateHostAsync(uri, cancellationToken);
            if (!allowed)
            {
                return Fail(error!);
            }

            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                return Fail($"The page returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var format = ClassifyContentType(response.Content.Headers.ContentType?.MediaType);
            if (format is null)
            {
                return Fail("That URL's content type isn't supported - try a web page, PDF, or plain text/markdown/CSV document.");
            }

            var bytes = await ReadWithLimitAsync(response, maxBytes, cancellationToken);
            if (bytes is null)
            {
                return Fail($"That page is too large - the limit is {maxBytes / 1_000_000.0:0.0} MB.");
            }

            return new UrlFetchResult { Success = true, Bytes = bytes, Format = format, FinalUrl = uri.ToString() };
        }

        return Fail("Too many redirects.");
    }

    private async Task<(bool Allowed, string? Error)> ValidateHostAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return (false, "Only http/https URLs are supported.");
        }

        if (IsAllowedInternalHost(uri.Host))
        {
            return (true, null);
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve host {Host} for URL fetch.", uri.Host);
            return (false, "Couldn't resolve that host.");
        }

        if (addresses.Length == 0)
        {
            return (false, "Couldn't resolve that host.");
        }

        if (addresses.Any(ip => BlockedRanges.Any(range => range.Contains(ip))))
        {
            return (false, "That URL points to an internal or restricted address and can't be fetched.");
        }

        return (true, null);
    }

    private bool IsAllowedInternalHost(string host) =>
        _allowedInternalHostSuffixes.Any(suffix =>
            host.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase));

    private static async Task<byte[]?> ReadWithLimitAsync(HttpResponseMessage response, long maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static string? ClassifyContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return "html";
        return contentType.ToLowerInvariant() switch
        {
            "text/html" or "application/xhtml+xml" => "html",
            "application/pdf" => "pdf",
            "text/plain" => "txt",
            "text/markdown" => "md",
            "text/csv" => "csv",
            "text/vtt" => "txt",
            var t when t.StartsWith("text/") => "html",
            _ => null
        };
    }

    private static UrlFetchResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
