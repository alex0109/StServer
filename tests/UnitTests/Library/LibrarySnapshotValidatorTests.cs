using System.Text.Json;
using Application.Constants;
using Application.Exceptions;
using Application.Library;
using Domain.Utility.Material;
using Domain.Utility.Question;
using Shouldly;

namespace UnitTests.Library;

public class LibrarySnapshotValidatorTests
{
    private static LibrarySnapshot Valid() => new()
    {
        Title = "German A1",
        Type = MaterialType.practice,
        Content = JsonDocument.Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Hallo"}]}]}""").RootElement,
        Notes = [new LibraryNoteSnapshot { Key = 1, Title = "Articles" }],
        Dictionaries =
        [
            new LibraryDictionarySnapshot
            {
                Title = "Words",
                Entries = [new LibraryEntrySnapshot { Key = 1, Term = "der Tisch", Translation = "стіл" }],
            },
        ],
        Questions =
        [
            new LibraryQuestionSnapshot
            {
                Title = "Choose",
                QuestionType = QuestionType.Options,
                QuestionDifficulty = QuestionDifficulty.Easy,
                Options =
                [
                    new LibraryOptionSnapshot { Name = "der", IsCorrect = true },
                    new LibraryOptionSnapshot { Name = "die" },
                ],
            },
            new LibraryQuestionSnapshot
            {
                Title = "True or not",
                QuestionType = QuestionType.TrueFalse,
                QuestionDifficulty = QuestionDifficulty.Medium,
                Options =
                [
                    new LibraryOptionSnapshot { Name = QuestionLimits.TrueOptionName, IsCorrect = true },
                    new LibraryOptionSnapshot { Name = QuestionLimits.FalseOptionName },
                ],
            },
            new LibraryQuestionSnapshot
            {
                Title = "Translate",
                Answer = "der Tisch",
                QuestionType = QuestionType.Open,
                QuestionDifficulty = QuestionDifficulty.Hard,
                NoteKey = 1,
                EntryKey = 1,
            },
        ],
    };

    [Fact]
    public void AValidMaterial_HasNoProblems()
    {
        LibrarySnapshotValidator.FindProblems(Valid()).ShouldBeEmpty();
    }

    public static TheoryData<Action<LibrarySnapshot>, string> Broken => new()
    {
        { s => s.Title = new string('a', 71), "Title is longer" },
        { s => s.Title = "  ", "Title is empty" },
        { s => s.Link = "javascript:alert(1)", "Link" },
        { s => s.Content = JsonDocument.Parse("""{"type":"doc","content":[{"text":"no type"}]}""").RootElement, "not an editor document" },
        { s => s.Notes[0].Title = "", "no title" },
        { s => s.Notes.Add(new LibraryNoteSnapshot { Key = 1, Title = "Twin" }), "the key is used twice" },
        { s => s.Dictionaries[0].Entries[0].Term = new string('a', 201), "term is longer" },
        { s => s.Dictionaries[0].Entries[0].Translation = "", "translation is empty" },
        { s => s.Questions[0].Title = new string('a', 101), "title is longer" },
        { s => s.Questions[0].Options = [new LibraryOptionSnapshot { Name = "only", IsCorrect = true }], "options" },
        { s => s.Questions[0].Options.ForEach(x => x.IsCorrect = false), "no correct option" },
        { s => s.Questions[1].Options[0].Name = "Yes", "needs the options" },
        { s => s.Questions[1].Options.ForEach(x => x.IsCorrect = true), "exactly one correct" },
        { s => s.Questions[2].Answer = " ", "no answer" },
        { s => s.Questions[2].NoteKey = 9, "no note 9" },
        { s => s.Questions[2].EntryKey = 9, "no word 9" },
    };

    [Theory]
    [MemberData(nameof(Broken))]
    public void ABrokenField_IsNamed(Action<LibrarySnapshot> breakIt, string expected)
    {
        var snapshot = Valid();
        breakIt(snapshot);

        LibrarySnapshotValidator.FindProblems(snapshot)
            .ShouldContain(problem => problem.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ManyProblems_AreCutButCounted()
    {
        var snapshot = Valid();
        snapshot.Dictionaries[0].Entries = Enumerable.Range(1, 15)
            .Select(key => new LibraryEntrySnapshot { Key = key, Term = "", Translation = "x" })
            .ToList();
        snapshot.Questions.RemoveAt(2);

        var error = Should.Throw<BadRequestException>(() => LibrarySnapshotValidator.EnsureValid(snapshot));

        error.Message.ShouldContain("and 5 more");
    }
}
