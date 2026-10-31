using System.Text.RegularExpressions;

namespace Application.Ai;

// A language typed by the user goes into prompts, so it must be a plain name and nothing else
public static partial class AiLanguageName
{
    [GeneratedRegex(@"^\p{L}[\p{L} \-]{1,39}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);
}
