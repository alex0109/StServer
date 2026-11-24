namespace Application.Ai;

// Rules of AI usage that do not depend on a provider
public class AiOptions
{
    public const string SectionName = "Ai";

    // Kill switch: all AI features answer "unavailable" when false
    public bool Enabled { get; set; } = true;

    // Providers are tried in this order until one answers, names match IAiProvider.Name
    public string[]? ProviderOrder { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 120;
    // Watching a video takes longer; still under the 230 s Azure allows one HTTP request
    public int VideoTimeoutSeconds { get; set; } = 200;

    // Fuzzy score from this value up to the fuzzy pass score is the "gray zone" checked by AI
    public int GradingGrayZoneMin { get; set; } = 40;
    // AI score from this value counts as a correct answer
    public int GradingPassScore { get; set; } = 70;

    // Answers a provider gave (and billed) but the user can't use: "no new words", a video without
    // speech, text that isn't the asked format. This many a day per user and feature are forgiven,
    // later ones count like a usable answer, or an empty answer would be a free request without end
    public int UnusableGracePerDay { get; set; } = 3;

    public static readonly string[] DefaultProviderOrder = ["Anthropic", "OpenAi", "Gemini"];
}
