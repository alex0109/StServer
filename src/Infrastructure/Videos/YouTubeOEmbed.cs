using System.Net.Http.Json;
using Application.Interfaces;
using Application.Videos;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Videos;

// YouTube's public oEmbed endpoint: the title of a public video, no key and no page scraping
public sealed class YouTubeOEmbed : IVideoInfo
{
    public const string HttpClientName = "YouTubeOEmbed";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YouTubeOEmbed> _logger;

    public YouTubeOEmbed(IHttpClientFactory httpClientFactory, ILogger<YouTubeOEmbed> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> GetTitleAsync(string videoId, CancellationToken cancellationToken = default)
    {
        var url = $"https://www.youtube.com/oembed?format=json&url={Uri.EscapeDataString(YouTubeLink.WatchUrl(videoId))}";
        try
        {
            var answer = await _httpClientFactory.CreateClient(HttpClientName)
                .GetFromJsonAsync<OEmbed>(url, cancellationToken);
            var title = answer?.Title?.Trim();
            return string.IsNullOrEmpty(title) ? null : title;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // A material is made without the title rather than not at all
            _logger.LogInformation("YouTube title of {VideoId} is unknown: {Error}", videoId, exception.GetType().Name);
            return null;
        }
    }

    private sealed record OEmbed(string? Title);
}
