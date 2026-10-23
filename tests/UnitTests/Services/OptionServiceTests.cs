using Application.Constants;
using Application.DTOs.Option;
using Application.Exceptions;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Question;
using Shouldly;

namespace UnitTests.Services;

public class OptionServiceTests
{
    private readonly OptionService _sut = new();

    private static Question CreateQuestion(params string[] optionNames)
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            Title = "Question",
            QuestionType = QuestionType.Options,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        question.Options = optionNames
            .Select((name, index) => new Option
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Name = name,
                Position = index
            })
            .ToList();

        question.CorrectOptionId = question.Options.FirstOrDefault()?.Id;

        return question;
    }

    [Fact]
    public void SyncOptions_NewOptions_AddsThemInOrderAndSetsCorrect()
    {
        var question = CreateQuestion();

        _sut.SyncOptions(question,
        [
            new OptionUpdateDto { Name = " A " },
            new OptionUpdateDto { Name = "B", IsCorrect = true },
            new OptionUpdateDto { Name = "C" }
        ]);

        var options = question.Options.OrderBy(x => x.Position).ToList();
        options.Select(x => x.Name).ShouldBe(["A", "B", "C"]);
        options.Select(x => x.Position).ShouldBe([0, 1, 2]);
        question.CorrectOptionId.ShouldBe(options[1].Id);
    }

    [Fact]
    public void SyncOptions_ExistingOptions_RenamesReordersAndRemovesMissing()
    {
        var question = CreateQuestion("A", "B", "C");
        var a = question.Options.Single(x => x.Name == "A");
        var c = question.Options.Single(x => x.Name == "C");

        _sut.SyncOptions(question,
        [
            new OptionUpdateDto { Id = c.Id, IsCorrect = true },
            new OptionUpdateDto { Id = a.Id, Name = "A2" }
        ]);

        question.Options.Count.ShouldBe(2);
        c.Position.ShouldBe(0);
        a.Position.ShouldBe(1);
        a.Name.ShouldBe("A2");
        c.Name.ShouldBe("C");
        question.CorrectOptionId.ShouldBe(c.Id);
    }

    [Fact]
    public void SyncOptions_TooFewOptions_ThrowsAndKeepsQuestion()
    {
        var question = CreateQuestion("A", "B");

        Should.Throw<BadRequestException>(() => _sut.SyncOptions(question,
        [
            new OptionUpdateDto { Name = "A", IsCorrect = true }
        ]));

        question.Options.Count.ShouldBe(2);
    }

    [Fact]
    public void SyncOptions_TooManyOptions_Throws()
    {
        var options = Enumerable.Range(0, QuestionLimits.MaxOptions + 1)
            .Select(i => new OptionUpdateDto { Name = $"Option {i}", IsCorrect = i == 0 })
            .ToList();

        Should.Throw<BadRequestException>(() => _sut.SyncOptions(CreateQuestion(), options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void SyncOptions_NotExactlyOneCorrect_Throws(int correctCount)
    {
        var options = new List<OptionUpdateDto>
        {
            new() { Name = "A", IsCorrect = correctCount >= 1 },
            new() { Name = "B", IsCorrect = correctCount >= 2 },
            new() { Name = "C" }
        };

        Should.Throw<BadRequestException>(() => _sut.SyncOptions(CreateQuestion(), options));
    }

    [Fact]
    public void SyncOptions_DuplicateNames_Throws()
    {
        Should.Throw<BadRequestException>(() => _sut.SyncOptions(CreateQuestion(),
        [
            new OptionUpdateDto { Name = "Paris", IsCorrect = true },
            new OptionUpdateDto { Name = "paris " }
        ]));
    }

    [Fact]
    public void SyncOptions_EmptyName_Throws()
    {
        Should.Throw<BadRequestException>(() => _sut.SyncOptions(CreateQuestion(),
        [
            new OptionUpdateDto { Name = "A", IsCorrect = true },
            new OptionUpdateDto { Name = "  " }
        ]));
    }

    [Fact]
    public void SyncOptions_UnknownOptionId_Throws()
    {
        Should.Throw<BadRequestException>(() => _sut.SyncOptions(CreateQuestion("A", "B"),
        [
            new OptionUpdateDto { Id = Guid.NewGuid(), Name = "A", IsCorrect = true },
            new OptionUpdateDto { Name = "B" }
        ]));
    }

    [Theory]
    [InlineData(true, QuestionLimits.TrueOptionName)]
    [InlineData(false, QuestionLimits.FalseOptionName)]
    public void SetTrueFalseOptions_CreatesTwoOptionsAndMarksCorrect(bool isTrue, string correctName)
    {
        var question = CreateQuestion();

        _sut.SetTrueFalseOptions(question, isTrue);

        var options = question.Options.OrderBy(x => x.Position).ToList();
        options.Select(x => x.Name).ShouldBe([QuestionLimits.TrueOptionName, QuestionLimits.FalseOptionName]);
        question.CorrectOptionId.ShouldBe(options.Single(x => x.Name == correctName).Id);
    }

    [Fact]
    public void SetCorrectOption_OptionOfQuestion_SetsIt()
    {
        var question = CreateQuestion("A", "B");
        var b = question.Options.Single(x => x.Name == "B");

        _sut.SetCorrectOption(question, b.Id);

        question.CorrectOptionId.ShouldBe(b.Id);
    }

    [Fact]
    public void SetCorrectOption_ForeignOption_Throws()
    {
        var question = CreateQuestion("A", "B");
        var correctBefore = question.CorrectOptionId;

        Should.Throw<BadRequestException>(() => _sut.SetCorrectOption(question, Guid.NewGuid()));

        question.CorrectOptionId.ShouldBe(correctBefore);
    }
}
