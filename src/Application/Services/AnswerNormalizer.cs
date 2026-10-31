using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Application.Interfaces;

namespace Application.Services;

// Brings an answer to a form where only meaningful differences remain:
// case, punctuation, accents, apostrophe styles and spacing never make an answer wrong
public partial class AnswerNormalizer : IAnswerNormalizer
{
    public string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // Full width letters, ligatures and similar forms become their plain letters
        value = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();

        value = RemoveLatinAccents(value)
            .Replace("ß", "ss")
            .Replace('ё', 'е');

        // "пʼять", "п'ять" and "п’ять" are the same word
        value = ApostropheRegex().Replace(value, "");

        // Joined and separated forms compare as words: "well-known", "km/h", "snake_case"
        value = SeparatorRegex().Replace(value, " ");

        value = NonWordRegex().Replace(value, "");

        return SpacesRegex().Replace(value, " ").Trim();
    }

    // Accents are dropped only on Latin letters: in Cyrillic "й" and "ї" are letters of their own
    private static string RemoveLatinAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousIsLatin = false;

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                if (!previousIsLatin) builder.Append(c);
                continue;
            }

            previousIsLatin = c <= 'ɏ';
            builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"['’‘ʼʻ`´]")]
    private static partial Regex ApostropheRegex();

    [GeneratedRegex(@"[-‐‑‒–—―/\\_|]")]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}\s]")]
    private static partial Regex NonWordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesRegex();
}
