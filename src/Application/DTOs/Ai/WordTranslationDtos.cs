namespace Application.DTOs.Ai;

public class WordTranslateDto
{
    public string? Term { get; set; }
    // A plain language name, e.g. "Ukrainian"; empty: the user's native or interface language
    public string? Language { get; set; }
}

// Why no translation came: the user switched it off, it's a Plus extra, the quota is used up, or AI is down
public enum WordTranslationSkip
{
    Off = 1,
    Plan = 2,
    Quota = 3,
    Unavailable = 4,
}

public record WordTranslationDto(string? Translation, WordTranslationSkip? Skipped)
{
    public static WordTranslationDto Skip(WordTranslationSkip reason) => new(null, reason);
}
