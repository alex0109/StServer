using Domain.Utility.Dictionary;

namespace Application.Ai.Prompts;

// Parts of speech as the model sees them: lowercase enum names, "other" when none fits
public static class AiPartsOfSpeech
{
    public const string Other = "other";

    public static readonly string[] Values =
        [.. Enum.GetNames<PartOfSpeech>().Select(x => x.ToLowerInvariant()), Other];

    // Names only: a number from the model is not a part of speech
    public static PartOfSpeech? Parse(string? value) =>
        value is not null
        && !value.Any(char.IsDigit)
        && Enum.TryParse<PartOfSpeech>(value, ignoreCase: true, out var parsed)
            ? parsed
            : null;
}
