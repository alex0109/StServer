using System.Text.Json;
using Application.Constants;
using Application.Essays;
using Application.DTOs.RichTextDocument;
using Application.Exceptions;
using Domain.Utility.Note;
using Domain.Utility.Question;

namespace Application.Library;

// A snapshot usually comes from a material that already passed every check. An imported one comes
// from a file, and a field that is too long would only surface later: when a user adds the material
// and the database refuses the copy
public static class LibrarySnapshotValidator
{
    // The columns a copy lands in
    private const int MaxMaterialTitle = 70;
    private const int MaxQuestionTitle = 100;
    private const int MaxAnswer = 1000;
    private const int MaxExplanation = 1000;
    private const int MaxOption = 500;
    private const int MaxDictionaryTitle = 70;
    private const int MaxDictionaryDescription = 500;
    private const int MaxTerm = 200;
    private const int MaxTranslation = 200;
    private const int MaxTranscription = 200;
    private const int MaxArticle = 20;
    private const int MaxGrammarInfo = 300;
    private const int MaxExample = 500;
    private const int MaxEntryNote = 1000;
    private const int MaxTextNode = 5000;

    private const int ProblemsShown = 10;

    public static void EnsureValid(LibrarySnapshot snapshot)
    {
        var problems = FindProblems(snapshot);
        if (problems.Count == 0) return;

        var more = problems.Count > ProblemsShown ? $" (and {problems.Count - ProblemsShown} more)" : string.Empty;
        throw new BadRequestException($"{string.Join("; ", problems.Take(ProblemsShown))}{more}");
    }

    public static List<string> FindProblems(LibrarySnapshot snapshot)
    {
        var problems = new List<string>();

        Required(snapshot.Title, MaxMaterialTitle, "Title", problems);
        if (snapshot.Link is not null && !LibrarySnapshotMapper.IsWebLink(snapshot.Link))
            problems.Add("Link must be a web address");
        CheckText(snapshot.Content, "Content", problems);

        var noteKeys = CheckNotes(snapshot, problems);
        var entryKeys = CheckDictionaries(snapshot, problems);

        foreach (var (question, index) in snapshot.Questions.Select((x, i) => (x, i + 1)))
            CheckQuestion(question, $"Question {index}", noteKeys, entryKeys, problems);

        return problems;
    }

    private static HashSet<int> CheckNotes(LibrarySnapshot snapshot, List<string> problems)
    {
        var keys = new HashSet<int>();

        foreach (var note in snapshot.Notes)
        {
            var label = $"Note {note.Key}";

            if (!keys.Add(note.Key)) problems.Add($"{label}: the key is used twice");
            if (string.IsNullOrWhiteSpace(note.Title)) problems.Add($"{label}: no title");
            // These depend on a file in storage, which a snapshot doesn't carry
            if (note.Type == NoteType.pdfAnnotation) problems.Add($"{label}: PDF notes can't be imported");

            CheckText(note.TextContent, label, problems);
            Optional(note.EssayTask, EssayRules.MaxTaskLength, $"{label} essay task", problems);
        }

        return keys;
    }

    private static HashSet<int> CheckDictionaries(LibrarySnapshot snapshot, List<string> problems)
    {
        var keys = new HashSet<int>();

        foreach (var (dictionary, index) in snapshot.Dictionaries.Select((x, i) => (x, i + 1)))
        {
            var label = $"Dictionary {index}";

            Required(dictionary.Title, MaxDictionaryTitle, $"{label} title", problems);
            Optional(dictionary.Description, MaxDictionaryDescription, $"{label} description", problems);

            foreach (var entry in dictionary.Entries)
            {
                var word = $"Word {entry.Key}";

                if (!keys.Add(entry.Key)) problems.Add($"{word}: the key is used twice");
                Required(entry.Term, MaxTerm, $"{word} term", problems);
                Required(entry.Translation, MaxTranslation, $"{word} translation", problems);
                Optional(entry.Transcription, MaxTranscription, $"{word} transcription", problems);
                Optional(entry.Article, MaxArticle, $"{word} article", problems);
                Optional(entry.GrammarInfo, MaxGrammarInfo, $"{word} grammar", problems);
                Optional(entry.Example, MaxExample, $"{word} example", problems);
                Optional(entry.ExampleTranslation, MaxExample, $"{word} example translation", problems);
                Optional(entry.Note, MaxEntryNote, $"{word} note", problems);
            }
        }

        return keys;
    }

    private static void CheckQuestion(
        LibraryQuestionSnapshot question, string label, HashSet<int> noteKeys, HashSet<int> entryKeys, List<string> problems)
    {
        Required(question.Title, MaxQuestionTitle, $"{label} title", problems);
        Optional(question.Answer, MaxAnswer, $"{label} answer", problems);
        Optional(question.Explanation, MaxExplanation, $"{label} explanation", problems);

        if (!Enum.IsDefined(question.QuestionDifficulty)) problems.Add($"{label}: unknown difficulty");
        if (question.NoteKey is int noteKey && !noteKeys.Contains(noteKey)) problems.Add($"{label}: no note {noteKey}");
        if (question.EntryKey is int entryKey && !entryKeys.Contains(entryKey)) problems.Add($"{label}: no word {entryKey}");

        foreach (var option in question.Options)
            Required(option.Name, MaxOption, $"{label} option", problems);

        var correct = question.Options.Count(x => x.IsCorrect);

        switch (question.QuestionType)
        {
            case QuestionType.Options:
                if (question.Options.Count is < QuestionLimits.MinOptions or > QuestionLimits.MaxOptions)
                    problems.Add($"{label}: needs {QuestionLimits.MinOptions} to {QuestionLimits.MaxOptions} options");
                if (correct == 0) problems.Add($"{label}: no correct option");
                break;
            case QuestionType.TrueFalse:
                var names = question.Options.Select(x => x.Name).ToHashSet();
                if (question.Options.Count != 2 || !names.Contains(QuestionLimits.TrueOptionName) || !names.Contains(QuestionLimits.FalseOptionName))
                    problems.Add($"{label}: needs the options {QuestionLimits.TrueOptionName} and {QuestionLimits.FalseOptionName}");
                if (correct != 1) problems.Add($"{label}: needs exactly one correct option");
                break;
            case QuestionType.Open:
                if (string.IsNullOrWhiteSpace(question.Answer)) problems.Add($"{label}: no answer");
                break;
            case QuestionType.Flashcard:
                if (question.EntryKey is null) problems.Add($"{label}: a flashcard needs a word");
                break;
            default:
                problems.Add($"{label}: unknown type");
                break;
        }
    }

    private static void CheckText(JsonElement? text, string label, List<string> problems)
    {
        if (text is not { ValueKind: JsonValueKind.Object } value) return;

        try
        {
            var document = value.Deserialize<RichTextDocument>(RichTextJson.Read);
            if (document?.Content.Any(TooLong) == true)
                problems.Add($"{label}: a block of text is longer than {MaxTextNode} characters");
        }
        catch (JsonException)
        {
            problems.Add($"{label}: not an editor document");
        }
    }

    private static bool TooLong(Node node) =>
        node.Text?.Length > MaxTextNode || node.Content?.Any(TooLong) == true;

    private static void Required(string? value, int max, string label, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(value)) problems.Add($"{label} is empty");
        else Optional(value, max, label, problems);
    }

    private static void Optional(string? value, int max, string label, List<string> problems)
    {
        if (value?.Length > max) problems.Add($"{label} is longer than {max} characters");
    }
}
