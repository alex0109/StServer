using FuzzySharp;

namespace Application.Evaluators;

// Similarity of two normalized answers.
// Score tolerates typos, word order, spacing and filler words; FactsMatch guards what a
// typo-tolerant score can't see: a different number or a negation turns a right answer wrong
public static class FuzzyAnswerMatcher
{
    // From this score an answer with the same facts counts as a typo of the right one
    public const int PassScore = 85;

    // Articles carry no meaning for a self-test answer
    private static readonly HashSet<string> FillerWords = ["a", "an", "the"];

    private static readonly HashSet<string> NegationWords =
    [
        // English, apostrophes are already removed: "don't" -> "dont"
        "not", "no", "never", "none", "nothing", "nobody", "neither", "nor", "without", "cannot",
        "cant", "dont", "doesnt", "didnt", "isnt", "arent", "wasnt", "werent", "wont", "wouldnt",
        "shouldnt", "couldnt", "hasnt", "havent", "hadnt", "aint",
        // Ukrainian
        "не", "ні", "ніколи", "ніхто", "ніщо", "нічого", "жоден", "жодна", "жодне", "жодного", "без",
        // Russian, German, Polish, French, Spanish
        "нет", "никогда", "ничего", "никто", "nicht", "kein", "keine", "keinen", "keiner", "nie",
        "niemals", "ohne", "nigdy", "bez", "pas", "jamais", "sans", "nunca", "sin",
    ];

    // Small numbers are often written as words, "3 states" and "three states" are the same answer
    private static readonly Dictionary<string, string> NumberWords = new()
    {
        ["zero"] = "0", ["one"] = "1", ["two"] = "2", ["three"] = "3", ["four"] = "4", ["five"] = "5",
        ["six"] = "6", ["seven"] = "7", ["eight"] = "8", ["nine"] = "9", ["ten"] = "10",
        ["eleven"] = "11", ["twelve"] = "12", ["twenty"] = "20", ["hundred"] = "100", ["thousand"] = "1000",
        ["нуль"] = "0", ["один"] = "1", ["одна"] = "1", ["одне"] = "1", ["два"] = "2", ["дві"] = "2",
        ["три"] = "3", ["чотири"] = "4", ["пять"] = "5", ["шість"] = "6", ["сім"] = "7", ["вісім"] = "8",
        ["девять"] = "9", ["десять"] = "10", ["двадцять"] = "20", ["сто"] = "100", ["тисяча"] = "1000",
    };

    public static FuzzyMatch Compare(string correctAnswer, string userAnswer)
    {
        var correct = Tokenize(correctAnswer);
        var user = Tokenize(userAnswer);

        if (correct.Length == 0 || user.Length == 0)
            return new FuzzyMatch(0, false);

        var correctText = string.Join(' ', correct);
        var userText = string.Join(' ', user);

        var score = Math.Max(
            Fuzz.TokenSortRatio(correctText, userText),
            // "photo synthesis" and "photosynthesis"
            Fuzz.Ratio(string.Concat(correct), string.Concat(user)));

        var correctMeaningful = correct.Where(x => !FillerWords.Contains(x)).ToArray();
        var userMeaningful = user.Where(x => !FillerWords.Contains(x)).ToArray();

        if (correctMeaningful.Length > 0 && userMeaningful.Length > 0)
        {
            score = Math.Max(score, Fuzz.TokenSortRatio(
                string.Join(' ', correctMeaningful),
                string.Join(' ', userMeaningful)));
        }

        var factsMatch =
            Numbers(correct).SequenceEqual(Numbers(user))
            && HasNegation(correct) == HasNegation(user);

        return new FuzzyMatch(score, factsMatch);
    }

    // Answers come normalized from the evaluation service, lower case keeps direct calls safe too
    private static string[] Tokenize(string answer) =>
        answer
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => NumberWords.GetValueOrDefault(x, x))
            .ToArray();

    private static IEnumerable<string> Numbers(IEnumerable<string> tokens) =>
        tokens
            .Where(x => x.All(char.IsDigit))
            .Select(x => x.TrimStart('0') is { Length: > 0 } trimmed ? trimmed : "0")
            .Order(StringComparer.Ordinal);

    private static bool HasNegation(IEnumerable<string> tokens) => tokens.Any(NegationWords.Contains);
}

public readonly record struct FuzzyMatch(int Score, bool FactsMatch)
{
    public bool IsMatch => FactsMatch && Score >= FuzzyAnswerMatcher.PassScore;
}
