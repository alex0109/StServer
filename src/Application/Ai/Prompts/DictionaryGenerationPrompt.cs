using Domain.Utility.Ai;
using Domain.Utility.Dictionary;

namespace Application.Ai.Prompts;

public record GeneratedEntry(
    string Term,
    string Translation,
    string? Transcription,
    PartOfSpeech? PartOfSpeech,
    string? Example,
    string? ExampleTranslation);

public class DictionaryGenerationPrompt : IAiPrompt<IReadOnlyList<GeneratedEntry>>
{
    public const int MaxContentLength = 24000;

    private readonly string _content;
    private readonly int _count;
    private readonly string _language;
    private readonly IReadOnlyCollection<string> _existingTerms;
    private readonly string _tag = AiContentSanitizer.NewContentTag();

    // Language comes from a validated short name, never from free user text
    public DictionaryGenerationPrompt(string content, int count, string language, IReadOnlyCollection<string> existingTerms)
    {
        _content = AiContentSanitizer.Clean(content, MaxContentLength);
        _count = count;
        _language = language;
        _existingTerms = existingTerms.Select(x => AiContentSanitizer.Clean(x, 100)).Take(150).ToList();
    }

    public AiFeature Feature => AiFeature.DictionaryGeneration;

    public AiRequest BuildRequest()
    {
        var existing = _existingTerms.Count == 0 ? "none" : string.Join(", ", _existingTerms);

        var userPrompt = $"""
            Pick up to {_count} words or short terms from the material below that are worth learning,
            and translate each into {_language}.

            Requirements:
            - "term": the word in its dictionary form, in the language it has in the material, at most 60 characters.
            - "translation": the meaning in {_language}, at most 100 characters.
            - "transcription": IPA in square brackets when the term is not in {_language}, otherwise "".
            - "partOfSpeech": one of the allowed values, "other" when none fits.
            - "example": a short sentence with the term, in the term's language, at most 150 characters.
            - "exampleTranslation": that sentence in {_language}, at most 150 characters.
            - Prefer terms that carry the key ideas. No names of people, no numbers.
            - Skip terms already in the dictionary: {existing}

            The material is inside the data tags:
            {AiContentSanitizer.Wrap(_tag, _content)}
            """;

        var entry = AiJson.Object(
            ("term", AiJson.String()),
            ("translation", AiJson.String()),
            ("transcription", AiJson.String()),
            ("partOfSpeech", AiJson.Enum(AiPartsOfSpeech.Values)),
            ("example", AiJson.String()),
            ("exampleTranslation", AiJson.String()));

        return new AiRequest
        {
            Feature = Feature,
            SystemPrompt = AiPromptRules.System("a language teacher who builds vocabulary lists", _tag),
            UserPrompt = userPrompt,
            OutputSchema = AiJson.ToElement(AiJson.Object(("entries", AiJson.Array(entry)))),
            MaxOutputTokens = 16000,
            Effort = AiEffort.Medium,
        };
    }

    public IReadOnlyList<GeneratedEntry> Parse(string json)
    {
        var answer = AiJson.Read<Answer>(json);
        var seen = new HashSet<string>(_existingTerms, StringComparer.OrdinalIgnoreCase);
        var result = new List<GeneratedEntry>();

        foreach (var item in answer.Entries ?? [])
        {
            var term = AiJson.Limit(item.Term, 200);
            var translation = AiJson.Limit(item.Translation, 200);
            if (term.Length == 0 || translation.Length == 0 || !seen.Add(term)) continue;

            var partOfSpeech = AiPartsOfSpeech.Parse(item.PartOfSpeech);

            result.Add(new GeneratedEntry(
                term,
                translation,
                NullIfEmpty(AiJson.Limit(item.Transcription, 200)),
                partOfSpeech,
                NullIfEmpty(AiJson.Limit(item.Example, 500)),
                NullIfEmpty(AiJson.Limit(item.ExampleTranslation, 500))));

            if (result.Count == _count) break;
        }

        return result;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private record Answer(List<AnswerEntry>? Entries);

    private record AnswerEntry(
        string? Term,
        string? Translation,
        string? Transcription,
        string? PartOfSpeech,
        string? Example,
        string? ExampleTranslation);
}
