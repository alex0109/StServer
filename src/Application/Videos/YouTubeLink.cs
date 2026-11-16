using System.Text.RegularExpressions;

namespace Application.Videos;

// Reads the video id out of the usual YouTube addresses: watch, youtu.be, shorts, embed, live
public static partial class YouTubeLink
{
    private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com",
        "youtube-nocookie.com", "www.youtube-nocookie.com", "youtu.be", "www.youtu.be",
    };

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex IdPattern();

    public static string? VideoId(string? link)
    {
        if (string.IsNullOrWhiteSpace(link)) return null;
        if (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("https" or "http") || !Hosts.Contains(uri.Host)) return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var id = uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase)
            ? segments.FirstOrDefault()
            : segments switch
            {
                ["watch", ..] => QueryValue(uri.Query, "v"),
                ["shorts" or "embed" or "live" or "v", var value, ..] => value,
                _ => null,
            };

        return id is not null && IdPattern().IsMatch(id) ? id : null;
    }

    // The address the model is given: always the plain watch page
    public static string WatchUrl(string videoId) => $"https://www.youtube.com/watch?v={videoId}";

    private static string? QueryValue(string query, string name) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2 && pair[0] == name)
            .Select(pair => Uri.UnescapeDataString(pair[1]))
            .FirstOrDefault();
}
