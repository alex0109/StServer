using Domain.Utility.Ai;
using Domain.Utility.Question;

namespace Application.Ai.Prompts;

public record GeneratedOption(string Name, bool IsCorrect);

public record GeneratedQuestion(
    QuestionType Type,
    string Title,
    QuestionDifficulty Difficulty,
    string? Answer,
    IReadOnlyList<GeneratedOption> Options,
    bool IsTrue,
    string? Explanation);

public class QuestionGenerationPrompt : IAiPrompt<IReadOnlyList<GeneratedQuestion>>
{
    public const int MaxContentLength = 24000;

    private readonly string _materialTitle;
    private readonly string _content;
    private readonly int _count;
    private readonly QuestionType? _type;
    private readonly IReadOnlyCollection<string> _existingTitles;
    private readonly string _tag = AiContentSanitizer.NewContentTag();

    public QuestionGenerationPrompt(
        string materialTitle,
        string content,
        int count,
        QuestionType? type,
        IReadOnlyCollection<string> existingTitles)
    {
        _materialTitle = AiContentSanitizer.Clean(materialTitle, 200);
        _content = AiContentSanitizer.Clean(content, MaxContentLength);
        _count = count;
        _type = type;
        _existingTitles = existingTitles.Select(x => AiContentSanitizer.Clean(x, 200)).Take(50).ToList();
    }

    public AiFeature Feature => AiFeature.QuestionGeneration;

    public AiRequest BuildRequest()
    {
        var types = _type switch
        {
            QuestionType.Open => "only \"open\" questions",
            QuestionType.Options => "only \"options\" questions",
            QuestionType.TrueFalse => "only \"trueFalse\" statements",
            _ => "a mix of \"open\", \"options\" and \"trueFalse\" questions",
        };

        var existing = _existingTitles.Count == 0
            ? "none"
            : string.Join("\n", _existingTitles.Select(x => "- " + x));

        var userPrompt = $"""
            Write {_count} study questions that check understanding of the material below. Use {types}.

            Requirements:
            - Every question must be answerable from the material alone and check an important idea, not trivia.
            - Write in the language of the material.
            - "title": the question, at most 100 characters. For "trueFalse" it is a statement.
            - "open": "answer" is a short model answer, at most 300 characters.
            - "options": 3 to 5 options of at most 100 characters, at least one correct, mark every option's "isCorrect".
            - "trueFalse": "isTrue" says whether the statement is true. Mix true and false statements.
            - "difficulty": easy, medium or hard.
            - "explanation": one sentence why the answer is right, at most 200 characters.
            - Fields that do not apply to a type: "" for text, [] for options, false for isTrue.

            Do not repeat these existing questions:
            {existing}

            The material is inside the data tags. Its title, also user data: "{_materialTitle}"
            {AiContentSanitizer.Wrap(_tag, _content)}
            """;

        var option = AiJson.Object(("text", AiJson.String()), ("isCorrect", AiJson.Boolean()));
        var question = AiJson.Object(
            ("type", AiJson.Enum("open", "options", "trueFalse")),
            ("title", AiJson.String()),
            ("difficulty", AiJson.Enum("easy", "medium", "hard")),
            ("answer", AiJson.String()),
            ("options", AiJson.Array(option)),
            ("isTrue", AiJson.Boolean()),
            ("explanation", AiJson.String()));

        return new AiRequest
        {
            Feature = Feature,
            SystemPrompt = AiPromptRules.System("a teacher who writes self-test questions", _tag),
            UserPrompt = userPrompt,
            OutputSchema = AiJson.ToElement(AiJson.Object(("questions", AiJson.Array(question)))),
            MaxOutputTokens = 16000,
            Effort = AiEffort.Medium,
        };
    }

    public IReadOnlyList<GeneratedQuestion> Parse(string json)
    {
        var answer = AiJson.Read<Answer>(json);
        var result = new List<GeneratedQuestion>();

        foreach (var item in answer.Questions ?? [])
        {
            var title = AiJson.Limit(item.Title, 100);
            if (title.Length == 0) continue;

            var difficulty = item.Difficulty switch
            {
                "easy" => QuestionDifficulty.Easy,
                "hard" => QuestionDifficulty.Hard,
                _ => QuestionDifficulty.Medium,
            };
            var explanation = AiJson.Limit(item.Explanation, 1000);

            switch (item.Type)
            {
                case "open":
                    var modelAnswer = AiJson.Limit(item.Answer, 1000);
                    if (modelAnswer.Length == 0) continue;
                    result.Add(new GeneratedQuestion(QuestionType.Open, title, difficulty, modelAnswer, [], false, explanation));
                    break;

                case "options":
                    var options = (item.Options ?? [])
                        .Select(x => new GeneratedOption(AiJson.Limit(x.Text, 500), x.IsCorrect))
                        .Where(x => x.Name.Length > 0)
                        .DistinctBy(x => x.Name.ToLowerInvariant())
                        .Take(6)
                        .ToList();
                    // Same rules as manual creation: 2-6 options, at least one correct
                    if (options.Count < 2 || !options.Any(x => x.IsCorrect)) continue;
                    result.Add(new GeneratedQuestion(QuestionType.Options, title, difficulty, null, options, false, explanation));
                    break;

                case "trueFalse":
                    result.Add(new GeneratedQuestion(QuestionType.TrueFalse, title, difficulty, null, [], item.IsTrue, explanation));
                    break;
            }

            if (result.Count == _count) break;
        }

        return result;
    }

    private record Answer(List<AnswerQuestion>? Questions);

    private record AnswerQuestion(
        string? Type,
        string? Title,
        string? Difficulty,
        string? Answer,
        List<AnswerOption>? Options,
        bool IsTrue,
        string? Explanation);

    private record AnswerOption(string? Text, bool IsCorrect);
}
