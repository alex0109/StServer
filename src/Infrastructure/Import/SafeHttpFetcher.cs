using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Application.Import;

namespace Infrastructure.Import;

public interface IAddressResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class DnsAddressResolver : IAddressResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);
}

public sealed record FetchedPage(Uri Url, byte[] Body, string? Charset);

// Fetches a user-given address without letting it reach inside our network (SSRF). Every hop is
// checked: scheme, port, and every address the name resolves to. The socket then connects only to an
// address checked at that moment, so a name that changes its answer between check and connect
// (DNS rebinding) still can't point inside. Redirects are followed by hand and checked the same way
public sealed class SafeHttpFetcher
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxRedirects = 3;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static readonly string[] HtmlTypes = ["text/html", "application/xhtml+xml"];

    private readonly IAddressResolver _resolver;
    private readonly HttpClient _client;
    private readonly string _userAgent;
    private readonly TimeSpan _timeout;

    public SafeHttpFetcher(IAddressResolver resolver, HttpMessageHandler handler, string userAgent, TimeSpan? timeout = null)
    {
        _resolver = resolver;
        _userAgent = userAgent;
        _timeout = timeout ?? Timeout;
        // The fetch has its own timeout, so a slow page fails with a clear error
        _client = new HttpClient(handler, disposeHandler: false) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    }

    // No cookies, no proxy, no automatic redirects; the connection goes only to a checked address
    public static SocketsHttpHandler CreateHandler(IAddressResolver resolver) => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        ConnectCallback = (context, cancellationToken) => ConnectAsync(resolver, context.DnsEndPoint, cancellationToken),
    };

    public static async ValueTask<Stream> ConnectAsync(IAddressResolver resolver, DnsEndPoint endPoint, CancellationToken cancellationToken)
    {
        var address = await CheckedAddressAsync(resolver, endPoint.Host, cancellationToken);

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, endPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    // Every address of the name must be public: a name that also points inside is refused as a whole
    public static async Task<IPAddress> CheckedAddressAsync(IAddressResolver resolver, string host, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try
            {
                addresses = await resolver.ResolveAsync(host, cancellationToken);
            }
            catch (SocketException)
            {
                throw new ArticleImportException(ArticleImportError.Unreachable, "The site's name doesn't resolve");
            }
        }

        if (addresses.Length == 0)
            throw new ArticleImportException(ArticleImportError.Unreachable, "The site's name doesn't resolve");

        if (!addresses.All(AddressRules.IsPublic))
            throw new ArticleImportException(ArticleImportError.BlockedAddress, "This address can't be fetched");

        return addresses[0];
    }

    // Web pages only: http or https on their usual ports, no user and password in the address
    public static Uri CheckUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArticleImportException(ArticleImportError.InvalidUrl, "Only http and https links can be imported");

        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ArticleImportException(ArticleImportError.InvalidUrl, "Links with a user name can't be imported");

        if (uri.Port is not (80 or 443))
            throw new ArticleImportException(ArticleImportError.BlockedAddress, "Only the usual web ports can be fetched");

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (host.Length == 0
            || host == "localhost"
            || host.EndsWith(".localhost", StringComparison.Ordinal)
            || host.EndsWith(".local", StringComparison.Ordinal)
            || host.EndsWith(".internal", StringComparison.Ordinal))
            throw new ArticleImportException(ArticleImportError.BlockedAddress, "This address can't be fetched");

        return uri;
    }

    public async Task<FetchedPage> FetchAsync(Uri url, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        var token = timeout.Token;

        try
        {
            var current = url;
            for (var hop = 0; ; hop++)
            {
                CheckUri(current);
                // Early, for a clear answer; the connect callback checks again before the socket opens
                await CheckedAddressAsync(_resolver, current.IdnHost, token);

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.UserAgent.ParseAdd(_userAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml", 0.9));

                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    if (hop >= MaxRedirects)
                        throw new ArticleImportException(ArticleImportError.Unreachable, "The page redirects too many times");

                    current = new Uri(current, location);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    throw new ArticleImportException(ArticleImportError.Unreachable, $"The page answered {(int)response.StatusCode}");

                var contentType = response.Content.Headers.ContentType;
                if (contentType?.MediaType is not { } mediaType || !HtmlTypes.Contains(mediaType.ToLowerInvariant()))
                    throw new ArticleImportException(ArticleImportError.NotHtml, "The link is not a web page");

                if (response.Content.Headers.ContentLength > MaxBytes)
                    throw new ArticleImportException(ArticleImportError.TooLarge, "The page is too large");

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                return new FetchedPage(current, await ReadCappedAsync(stream, token), contentType.CharSet);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ArticleImportException(ArticleImportError.Timeout, "The site took too long to answer");
        }
        catch (HttpRequestException exception)
        {
            // A refusal from the connect callback comes wrapped
            if (exception.InnerException is ArticleImportException refused) throw refused;
            throw new ArticleImportException(ArticleImportError.Unreachable, "The site can't be reached");
        }
    }

    // Stops reading at the limit instead of trusting Content-Length: a compressed page can be huge inside
    private static async Task<byte[]> ReadCappedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0) return buffer.ToArray();

            if (buffer.Length + read > MaxBytes)
                throw new ArticleImportException(ArticleImportError.TooLarge, "The page is too large");

            buffer.Write(chunk, 0, read);
        }
    }
}
