using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Entities;

namespace Application.Essays;

public static class EssayRules
{
    public const int MaxTaskLength = Note.MaxEssayTaskLength;
    // About 3000 words; longer essays are checked by their first part
    public const int MaxEssayLength = 20000;
    // Fewer words give nothing to judge organisation or range on
    public const int MinWords = 30;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Words are runs of text with at least one letter or digit: dashes and stray punctuation don't count
    public static int CountWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Count(word => word.Any(char.IsLetterOrDigit));

    // Overall score: every criterion weighs the same
    public static int Overall(IEnumerable<EssayCriterionScore> criteria)
    {
        var scores = criteria.Select(x => x.Score).ToList();
        return scores.Count == 0 ? 0 : (int)Math.Round(scores.Average(), MidpointRounding.AwayFromZero);
    }

    public static string? NormalizeTask(string? task) =>
        string.IsNullOrWhiteSpace(task) ? null : task.Trim();

    public static JsonDocument ToDocument(EssayReview review) => JsonSerializer.SerializeToDocument(review, Json);

    // A stored review that no longer reads is treated as missing: the user can check again
    public static EssayReview? FromDocument(JsonDocument? document)
    {
        if (document is null) return null;

        try
        {
            return document.Deserialize<EssayReview>(Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
