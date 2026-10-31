using Application.Import;
using Application.Interfaces;

namespace Infrastructure.Import;

public sealed class ArticleImporter : IArticleImporter
{
    private static readonly string[] VideoHosts = ["youtube.com", "youtu.be", "youtube-nocookie.com", "vimeo.com", "tiktok.com"];

    private readonly SafeHttpFetcher _fetcher;

    public ArticleImporter(SafeHttpFetcher fetcher)
    {
        _fetcher = fetcher;
    }

    public async Task<ImportedArticle> ImportAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
            throw new ArticleImportException(ArticleImportError.InvalidUrl, "This is not a web address");

        SafeHttpFetcher.CheckUri(uri);

        // TODO(video): videos are not imported. There is no official API for the captions of other
        // people's YouTube videos, and scraping them is fragile and against the terms of service.
        // A possible later way: the user pastes the transcript, or uploads their own subtitles file
        if (IsVideoSite(uri))
            throw new ArticleImportException(ArticleImportError.VideoNotSupported, "Videos can't be imported yet");

        var page = await _fetcher.FetchAsync(uri, cancellationToken);
        return ArticleExtractor.Extract(page);
    }

    private static bool IsVideoSite(Uri uri)
    {
        var host = uri.IdnHost.ToLowerInvariant();
        return VideoHosts.Any(x => host == x || host.EndsWith("." + x, StringComparison.Ordinal));
    }
}
