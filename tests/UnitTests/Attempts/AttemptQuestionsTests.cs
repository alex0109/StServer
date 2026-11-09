using Application.Attempts;
using Domain.Entities;
using Domain.Utility.Question;
using Shouldly;

namespace UnitTests.Attempts;

public class AttemptQuestionsTests
{
    private static Question CreateQuestion(QuestionDifficulty difficulty = QuestionDifficulty.Easy) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Question",
        QuestionType = QuestionType.Open,
        QuestionDifficulty = difficulty,
        CreatedAt = DateTime.UtcNow,
    };

    private static List<Question> Mixed() =>
    [
        CreateQuestion(QuestionDifficulty.Easy),
        CreateQuestion(QuestionDifficulty.Easy),
        CreateQuestion(QuestionDifficulty.Medium),
        CreateQuestion(QuestionDifficulty.Medium),
        CreateQuestion(QuestionDifficulty.Hard),
        CreateQuestion(QuestionDifficulty.Hard),
    ];

    [Fact]
    public void Draw_NoLevelsAndNoCount_TakesEveryQuestion()
    {
        var questions = Mixed();

        var drawn = AttemptQuestions.Draw(questions, [], null, new Random(1));

        drawn.Order().ShouldBe(questions.Select(q => q.Id).Order());
    }

    [Fact]
    public void Draw_ChosenLevels_TakesOnlyThem()
    {
        var questions = Mixed();
        var allowed = questions
            .Where(q => q.QuestionDifficulty is QuestionDifficulty.Easy or QuestionDifficulty.Medium)
            .Select(q => q.Id)
            .ToHashSet();

        var drawn = AttemptQuestions.Draw(questions, [QuestionDifficulty.Easy, QuestionDifficulty.Medium], null, new Random(1));

        drawn.Count.ShouldBe(4);
        drawn.ShouldAllBe(id => allowed.Contains(id));
    }

    [Fact]
    public void Draw_Count_TakesThatManyDifferentQuestions()
    {
        var questions = Mixed();

        var drawn = AttemptQuestions.Draw(questions, [], 3, new Random(1));

        drawn.Count.ShouldBe(3);
        drawn.Distinct().Count().ShouldBe(3);
        drawn.ShouldAllBe(id => questions.Any(q => q.Id == id));
    }

    [Fact]
    public void Draw_CountAboveWhatMatches_TakesAllThatMatch()
    {
        var questions = Mixed();

        var drawn = AttemptQuestions.Draw(questions, [QuestionDifficulty.Hard], 10, new Random(1));

        drawn.Count.ShouldBe(2);
    }

    [Fact]
    public void Draw_NothingMatches_IsEmpty()
    {
        List<Question> questions = [CreateQuestion(QuestionDifficulty.Easy)];

        AttemptQuestions.Draw(questions, [QuestionDifficulty.Hard], null, new Random(1)).ShouldBeEmpty();
    }

    [Fact]
    public void Draw_DifferentAttempts_GetDifferentSets()
    {
        var questions = Enumerable.Range(0, 30).Select(_ => CreateQuestion()).ToList();
        var random = new Random(7);

        var sets = Enumerable.Range(0, 5)
            .Select(_ => string.Join(",", AttemptQuestions.Draw(questions, [], 5, random)))
            .ToHashSet();

        sets.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Of_KeepsTheAttemptOrder_AndSkipsQuestionsNoLongerActive()
    {
        var questions = Mixed();
        var removed = Guid.NewGuid();
        var attempt = new Attempt
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            QuestionIds = [questions[4].Id, removed, questions[0].Id, questions[2].Id],
        };

        var result = AttemptQuestions.Of(attempt, questions);

        result.Select(q => q.Id).ShouldBe([questions[4].Id, questions[0].Id, questions[2].Id]);
    }

    [Fact]
    public void Of_AttemptFromBeforeTheDraw_HasEveryActiveQuestionInAStableOrder()
    {
        var questions = Mixed();
        var attempt = new Attempt { Id = Guid.NewGuid(), StartedAt = DateTime.UtcNow };

        var first = AttemptQuestions.Of(attempt, questions).Select(q => q.Id).ToList();
        var reversed = AttemptQuestions.Of(attempt, Enumerable.Reverse(questions).ToList()).Select(q => q.Id).ToList();

        first.Order().ShouldBe(questions.Select(q => q.Id).Order());
        reversed.ShouldBe(first);
    }
}
