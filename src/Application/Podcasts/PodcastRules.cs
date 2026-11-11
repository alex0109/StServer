using Application.Profile;

namespace Application.Podcasts;

public static class PodcastRules
{
    // Shorter sources give nothing to talk about
    public const int MinSourceLength = 300;
    // About 10 minutes of speech; it also bounds what one podcast costs to voice
    public const int MaxScriptCharacters = 14000;
    public const int MaxLineLength = 400;
    public const int MaxLinesPerChapter = 40;
    // A recording that hangs this long is treated as failed, so it can be started again
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);
    // How long a link to the audio works; the page asks for a new one when it opens
    public static readonly TimeSpan AudioLinkLifetime = TimeSpan.FromHours(6);

    // Languages a podcast can be spoken in: the ones explanations can be written in
    public static bool IsLanguage(string? code) =>
        code is not null && ProfileLanguages.Explanation.ContainsKey(code);

    // About a minute per 2500 characters of source, between 3 and 10 minutes
    public static int TargetMinutes(int sourceLength) => Math.Clamp(sourceLength / 2500, 3, 10);

    public static string AudioPath(Guid userId, Guid podcastId, DateTime createdAt) =>
        $"podcasts/{userId}/{podcastId}-{createdAt:yyyyMMddHHmmss}.mp3";

    public static string UserPrefix(Guid userId) => $"podcasts/{userId}/";
}
