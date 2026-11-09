using Application.Questions;
using Domain.Entities;
using Domain.Utility.Question;
using Shouldly;

namespace UnitTests.Questions;

public class OptionOrderTests
{
    private static Question CreateQuestion(QuestionType type, int options)
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            Title = "Question",
            QuestionType = type,
            CreatedAt = DateTime.UtcNow,
        };

        for (var i = 0; i < options; i++)
            question.Options.Add(new Option { Id = Guid.NewGuid(), Name = $"Option {i}", Position = i, IsCorrect = i == 0 });

        return question;
    }

    [Fact]
    public void SameRun_GivesTheSameOrderEveryTime()
    {
        var question = CreateQuestion(QuestionType.Options, 4);
        var runId = Guid.NewGuid();

        var first = OptionOrder.For(runId, question).Select(o => o.Id);
        var second = OptionOrder.For(runId, question).Select(o => o.Id);

        second.ShouldBe(first);
    }

    [Fact]
    public void KeepsEveryOption()
    {
        var question = CreateQuestion(QuestionType.Options, 5);

        var ordered = OptionOrder.For(Guid.NewGuid(), question);

        ordered.Select(o => o.Id).Order().ShouldBe(question.Options.Select(o => o.Id).Order());
    }

    [Fact]
    public void NewRuns_MoveTheRightAnswerAround()
    {
        var question = CreateQuestion(QuestionType.Options, 4);

        var positions = Enumerable.Range(0, 50)
            .Select(_ => OptionOrder.For(Guid.NewGuid(), question).FindIndex(o => o.IsCorrect))
            .ToHashSet();

        positions.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void TrueFalse_KeepsItsOrder()
    {
        var question = CreateQuestion(QuestionType.TrueFalse, 2);

        var ordered = OptionOrder.For(Guid.NewGuid(), question);

        ordered.Select(o => o.Position).ShouldBe([0, 1]);
    }
}
