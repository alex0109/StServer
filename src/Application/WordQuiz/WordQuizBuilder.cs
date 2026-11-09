using Application.DTOs.Dictionary;
using Domain.Entities;
using Domain.Utility.Dictionary;

namespace Application.WordQuiz;

// A quick check right after a word was forgotten: pick its translation, or its synonym when it has
// some. Wrong choices come from the user's other words, the order given (its own dictionary first)
public static class WordQuizBuilder
{
    public const int Choices = 4;

    public static WordQuizDto Build(DictionaryEntry entry, IReadOnlyList<WordQuizCandidate> candidates, Random random)
    {
        var synonyms = entry.Synonyms
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        // A synonym is asked about half the time; without wrong choices for it the translation is asked
        var kinds = synonyms.Count > 0 && random.Next(2) == 0
            ? new[] { WordQuizKind.Synonym, WordQuizKind.Translation }
            : [WordQuizKind.Translation];

        foreach (var kind in kinds)
        {
            var answer = kind == WordQuizKind.Synonym ? synonyms[random.Next(synonyms.Count)] : entry.Translation.Trim();
            var wrong = WrongChoices(kind, entry, synonyms, answer, candidates);

            if (wrong.Count > 0)
                return Quiz(entry, kind, Shuffled(answer, wrong, random));
        }

        return Quiz(entry, WordQuizKind.Translation, []);
    }

    // A synonym is told apart from other words of the same language, a translation from other
    // translations. Anything that is also right for this word is never offered as wrong
    private static List<string> WrongChoices(
        WordQuizKind kind,
        DictionaryEntry entry,
        IReadOnlyCollection<string> synonyms,
        string answer,
        IEnumerable<WordQuizCandidate> candidates)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { answer };
        if (kind == WordQuizKind.Synonym)
        {
            taken.Add(entry.Term.Trim());
            taken.UnionWith(synonyms);
        }
        else
        {
            taken.Add(entry.Translation.Trim());
        }

        return candidates
            .Select(c => (kind == WordQuizKind.Synonym ? c.Term : c.Translation).Trim())
            .Where(text => text.Length > 0 && taken.Add(text))
            .Take(Choices - 1)
            .ToList();
    }

    private static List<WordQuizOptionDto> Shuffled(string answer, IEnumerable<string> wrong, Random random)
    {
        var options = wrong
            .Select(text => new WordQuizOptionDto { Text = text })
            .Append(new WordQuizOptionDto { Text = answer, IsCorrect = true })
            .ToArray();

        random.Shuffle(options);
        return options.ToList();
    }

    private static WordQuizDto Quiz(DictionaryEntry entry, WordQuizKind kind, List<WordQuizOptionDto> options) => new()
    {
        EntryId = entry.Id,
        Kind = kind,
        Term = entry.Term,
        Article = entry.Article,
        Options = options,
    };
}
