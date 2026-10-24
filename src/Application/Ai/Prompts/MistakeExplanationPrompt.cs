using System.Text;
using Domain.Utility.Ai;

namespace Application.Ai.Prompts;

public record MistakeItem(int Index, string Question, string CorrectAnswer, string StudentAnswer, string? Explanation);

// One request explains every wrong answer of an attempt
public class MistakeExplanationPrompt : IAiPrompt<IReadOnlyDictionary<int, string>>
{
    private const int MaxFieldLength = 1000;

    private readonly IReadOnlyList<MistakeItem> _items;
    private readonly string _tag = AiContentSanitizer.NewContentTag();

    public MistakeExplanationPrompt(IReadOnlyList<MistakeItem> items)
    {
        _items = items;
    }

    public AiFeature Feature => AiFeature.MistakeExplanation;

    public AiRequest BuildRequest()
    {
        var builder = new StringBuilder();
        foreach (var item in _items)
        {
            builder.AppendLine($"#{item.Index}");
            builder.AppendLine($"Question: {AiContentSanitizer.Clean(item.Question, MaxFieldLength)}");
            builder.AppendLine($"Correct answer: {AiContentSanitizer.Clean(item.CorrectAnswer, MaxFieldLength)}");
            builder.AppendLine($"Student answer: {AiContentSanitizer.Clean(item.StudentAnswer, MaxFieldLength)}");
            if (!string.IsNullOrWhiteSpace(item.Explanation))
                builder.AppendLine($"Teacher's note: {AiContentSanitizer.Clean(item.Explanation, MaxFieldLength)}");
            builder.AppendLine();
        }

        var userPrompt = $"""
            A student answered these self-test questions wrong or only partly right.
            For every item explain in 2-4 sentences what the mistake is and what the right idea is,
            so the student understands it instead of memorising the answer.

            Requirements:
            - Return one explanation per item, with the item's number as "index".
            - Write in the language of the question. Friendly and direct, no praise or filler.
            - At most 600 characters each.

            The items are inside the data tags:
            {AiContentSanitizer.Wrap(_tag, builder.ToString())}
            """;

        var explanation = AiJson.Object(("index", AiJson.Integer()), ("explanation", AiJson.String()));

        return new AiRequest
        {
            Feature = Feature,
            SystemPrompt = AiPromptRules.System("a patient tutor", _tag),
            UserPrompt = userPrompt,
            OutputSchema = AiJson.ToElement(AiJson.Object(("explanations", AiJson.Array(explanation)))),
            MaxOutputTokens = 16000,
            Effort = AiEffort.Medium,
        };
    }

    public IReadOnlyDictionary<int, string> Parse(string json)
    {
        var indexes = _items.Select(x => x.Index).ToHashSet();

        return (AiJson.Read<Answer>(json).Explanations ?? [])
            .Where(x => indexes.Contains(x.Index))
            .Select(x => (x.Index, Text: AiJson.Limit(x.Explanation, 1500)))
            .Where(x => x.Text.Length > 0)
            .DistinctBy(x => x.Index)
            .ToDictionary(x => x.Index, x => x.Text);
    }

    private record Answer(List<AnswerExplanation>? Explanations);

    private record AnswerExplanation(int Index, string? Explanation);
}
