using System.Net.Http.Json;
using Application.Interfaces;
using Application.Videos;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Videos;

// Public facts of a YouTube video without a key: the title from the oEmbed endpoint, the length
// from the watch page (the one fact oEmbed doesn't give)
public sealed partial class YouTubeOEmbed : IVideoInfo
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

    public async Task<int?> GetDurationSecondsAsync(string videoId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, YouTubeLink.WatchUrl(videoId));
            // Without it, visitors from the EU get the cookie consent page instead of the video
            request.Headers.Add("Cookie", "CONSENT=YES+1");
            request.Headers.Add("Accept-Language", "en");

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var page = await response.Content.ReadAsStringAsync(cancellationToken);
            var match = LengthSeconds().Match(page);
            return match.Success && int.TryParse(match.Groups[1].Value, out var seconds) && seconds > 0 ? seconds : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogInformation("YouTube length of {VideoId} is unknown: {Error}", videoId, exception.GetType().Name);
            return null;
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex("\"lengthSeconds\":\"(\\d+)\"")]
    private static partial System.Text.RegularExpressions.Regex LengthSeconds();

    private sealed record OEmbed(string? Title);
}
