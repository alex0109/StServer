using Application.Ai;
using Application.DTOs.Question;
using Application.Evaluators;
using Application.Interfaces;
using Application.Services;
using Domain.Utility.Result;
using Moq;
using Shouldly;

namespace UnitTests.Ai;

public class AnswerGradingTests
{
    private readonly Mock<IAnswerSemanticGrader> _grader = new();
    private readonly AnswerEvaluationService _sut;

    public AnswerGradingTests()
    {
        _sut = new AnswerEvaluationService(
            new AnswerNormalizer(),
            [new ExactValidationStep(), new FuzzyValidationStep()],
            _grader.Object,
            new AiOptions());
    }

    [Fact]
    public void UserExample_FallsIntoGrayZone()
    {
        var normalizer = new AnswerNormalizer();
        var match = FuzzyAnswerMatcher.Compare(
            normalizer.Normalize("Фотосинтез перетворює світло на хімічну енергію"),
            normalizer.Normalize("Рослини з світла роблять енергію"));

        match.Score.ShouldBeGreaterThanOrEqualTo(new AiOptions().GradingGrayZoneMin);
        match.IsMatch.ShouldBeFalse();
    }

    [Fact]
    public async Task GrayZone_AiAccepts_AnswerIsCorrect()
    {
        _grader.Setup(x => x.GradeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(92);

        var result = await _sut.EvaluateAnswerAsync(
            "Що таке фотосинтез?",
            "Фотосинтез перетворює світло на хімічну енергію",
            "Рослини з світла роблять енергію");

        result.IsCorrect.ShouldBeTrue();
        result.Score.ShouldBe(92);
        result.Method.ShouldBe(EvaluationMethod.AI);
    }

    [Fact]
    public async Task GrayZone_AiGivesPartialScore_AnswerIsWrongWithScore()
    {
        _grader.Setup(x => x.GradeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(55);

        var result = await _sut.EvaluateAnswerAsync(
            "Що таке фотосинтез?",
            "Фотосинтез перетворює світло на хімічну енергію",
            "Рослини з світла роблять енергію");

        result.IsCorrect.ShouldBeFalse();
        result.Score.ShouldBe(55);
        result.Method.ShouldBe(EvaluationMethod.AI);
    }

    [Fact]
    public async Task GrayZone_AiUnavailable_KeepsUsualResult()
    {
        _grader.Setup(x => x.GradeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var result = await _sut.EvaluateAnswerAsync(
            "Що таке фотосинтез?",
            "Фотосинтез перетворює світло на хімічну енергію",
            "Рослини з світла роблять енергію");

        result.IsCorrect.ShouldBeFalse();
        result.Method.ShouldBe(EvaluationMethod.None);
    }

    [Fact]
    public async Task CloseAnswerWithAnotherNumber_IsCheckedByAi()
    {
        _grader.Setup(x => x.GradeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var result = await _sut.EvaluateAnswerAsync(
            "When did World War II end?",
            "World War II ended in 1945",
            "World War II ended in 1946");

        result.IsCorrect.ShouldBeFalse();
        result.Method.ShouldBe(EvaluationMethod.AI);
        _grader.Verify(x => x.GradeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearlyRight_DoesNotCallAi()
    {
        var result = await _sut.EvaluateAnswerAsync("q", "Photosynthesis", "photosyntesis");

        result.IsCorrect.ShouldBeTrue();
        result.Method.ShouldBe(EvaluationMethod.Fuzzy);
        _grader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ClearlyWrong_DoesNotCallAi()
    {
        var result = await _sut.EvaluateAnswerAsync("q", "Photosynthesis turns light into chemical energy", "xyz");

        result.IsCorrect.ShouldBeFalse();
        _grader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NoGrader_WorksAsBefore()
    {
        var sut = new AnswerEvaluationService(new AnswerNormalizer(), [new ExactValidationStep(), new FuzzyValidationStep()]);

        var result = await sut.EvaluateAnswerAsync(
            "q",
            "Фотосинтез перетворює світло на хімічну енергію",
            "Рослини з світла роблять енергію");

        result.ShouldBeEquivalentTo(new AnswerEvaluationResult { IsCorrect = false, Score = 0, Method = EvaluationMethod.None });
    }
}
