using Application.WordQuiz;
using Domain.Entities;
using Domain.Utility.Dictionary;
using Shouldly;

namespace UnitTests.WordQuiz;

public class WordQuizBuilderTests
{
    // Next(n) always gives the same pick: 0 asks for a synonym when there is one, 1 for the translation
    private sealed class StubRandom(int pick) : Random
    {
        public override int Next(int maxValue) => Math.Min(pick, maxValue - 1);
    }

    private static DictionaryEntry Entry(string term, string translation, params string[] synonyms) => new()
    {
        Id = Guid.NewGuid(),
        Term = term,
        Translation = translation,
        PartOfSpeech = PartOfSpeech.Noun,
        Article = "der",
        Synonyms = [.. synonyms],
        CreatedAt = DateTime.UtcNow,
    };

    private static readonly List<WordQuizCandidate> Candidates =
    [
        new("Katze", "кішка"),
        new("Haus", "будинок"),
        new("Baum", "дерево"),
        new("Vogel", "птах"),
    ];

    [Fact]
    public void Translation_HasOneRightChoiceAndThreeWrongOnes()
    {
        var quiz = WordQuizBuilder.Build(Entry("Hund", "собака"), Candidates, new Random(1));

        quiz.Kind.ShouldBe(WordQuizKind.Translation);
        quiz.Term.ShouldBe("Hund");
        quiz.Article.ShouldBe("der");
        quiz.Options.Count.ShouldBe(WordQuizBuilder.Choices);
        quiz.Options.Single(o => o.IsCorrect).Text.ShouldBe("собака");
        quiz.Options.Where(o => !o.IsCorrect).Select(o => o.Text).ShouldBe(["кішка", "будинок", "дерево"], ignoreOrder: true);
    }

    [Fact]
    public void WrongChoices_ComeFromTheFirstCandidates()
    {
        // The word's own dictionary comes first, the other dictionaries after it
        var quiz = WordQuizBuilder.Build(Entry("Hund", "собака"), Candidates, new Random(1));

        quiz.Options.ShouldNotContain(o => o.Text == "птах");
    }

    [Fact]
    public void SameMeaning_IsNeverOfferedAsWrong()
    {
        List<WordQuizCandidate> candidates = [new("Köter", "Собака"), new("Katze", "кішка"), new("Mieze", "кішка")];

        var quiz = WordQuizBuilder.Build(Entry("Hund", "собака"), candidates, new Random(1));

        quiz.Options.Select(o => o.Text).ShouldBe(["собака", "кішка"], ignoreOrder: true);
    }

    [Fact]
    public void Synonym_IsPickedAmongOtherWordsOfTheSameLanguage()
    {
        List<WordQuizCandidate> candidates = [new("Köter", "пес"), new("Katze", "кішка"), new("Haus", "будинок")];

        var quiz = WordQuizBuilder.Build(Entry("Hund", "собака", "Köter"), candidates, new StubRandom(0));

        quiz.Kind.ShouldBe(WordQuizKind.Synonym);
        quiz.Options.Single(o => o.IsCorrect).Text.ShouldBe("Köter");
        // Another synonym of the word would be right too, so it isn't among the wrong ones
        quiz.Options.Where(o => !o.IsCorrect).Select(o => o.Text).ShouldBe(["Katze", "Haus"], ignoreOrder: true);
    }

    [Fact]
    public void Synonym_WithoutWrongChoices_AsksForTheTranslation()
    {
        List<WordQuizCandidate> candidates = [new("Köter", "пес")];

        var quiz = WordQuizBuilder.Build(Entry("Hund", "собака", "Köter"), candidates, new StubRandom(0));

        quiz.Kind.ShouldBe(WordQuizKind.Translation);
        quiz.Options.Select(o => o.Text).ShouldBe(["собака", "пес"], ignoreOrder: true);
    }

    [Fact]
    public void WordWithSynonyms_CanStillBeAskedForTheTranslation()
    {
        var quiz = WordQuizBuilder.Build(Entry("Hund", "собака", "Köter"), Candidates, new StubRandom(1));

        quiz.Kind.ShouldBe(WordQuizKind.Translation);
    }

    [Fact]
    public void NoOtherWords_NoChoices()
    {
        var quiz = WordQuizBuilder.Build(Entry("Hund", "собака"), [], new Random(1));

        quiz.Options.ShouldBeEmpty();
    }

    [Fact]
    public void RightChoice_MovesAround()
    {
        var entry = Entry("Hund", "собака");

        var positions = Enumerable.Range(0, 40)
            .Select(seed => WordQuizBuilder.Build(entry, Candidates, new Random(seed)).Options.FindIndex(o => o.IsCorrect))
            .ToHashSet();

        positions.Count.ShouldBeGreaterThan(1);
    }
}
