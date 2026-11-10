using Application.Mappers;
using Domain.Utility.Dictionary;

namespace Application.Ai.Prompts;

// Parts of speech as the model sees them: lowercase enum names, "other" when none fits
public static class AiPartsOfSpeech
{
    public const string Other = "other";

    public static readonly string[] Values =
        [.. Enum.GetNames<PartOfSpeech>().Select(x => x.ToLowerInvariant()), Other];

    // The article is a field of a noun, not a word of its own, so it is offered only with nouns
    public const string ArticleRule =
        "for a noun, the article learners memorise with it, in the term's language: the definite one " +
        "(der/die/das, le/la, el/la, il/lo/la), or the indefinite one where dictionaries give that " +
        "(en/ei/et in Norwegian, en/ett in Swedish). Empty for other parts of speech and for languages " +
        "without articles or grammatical gender, such as English or Ukrainian.";
    private const int MaxArticleLength = 20;

    // Names only: a number from the model is not a part of speech
    public static PartOfSpeech? Parse(string? value) =>
        value is not null
        && !value.Any(char.IsDigit)
        && Enum.TryParse<PartOfSpeech>(value, ignoreCase: true, out var parsed)
            ? parsed
            : null;

    // The name the model reads in a prompt, the same spelling as the values it may answer with
    public static string Name(PartOfSpeech partOfSpeech) => partOfSpeech.ToString().ToLowerInvariant();

    // The model's article, kept only for a noun
    public static string? ArticleOf(PartOfSpeech? partOfSpeech, string? article) =>
        DictionaryMapper.ArticleOf(partOfSpeech, AiJson.Limit(article, MaxArticleLength));
}
