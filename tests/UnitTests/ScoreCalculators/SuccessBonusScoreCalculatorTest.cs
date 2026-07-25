using Application.DTOs.Result;
using Application.ScoreCalculators;
using Shouldly;

namespace UnitTests.ScoreCalculators;

public class SuccessBonusScoreCalculatorTests
{
    private readonly SuccessBonusScoreCalculator _sut = new();

    [Fact]
    public void Calculate_EmptyResults_ReturnsZero()
    {
        var result = _sut.Calculate(Enumerable.Empty<ResultResponseDto>());

        result.ShouldBe(0);
    }

    [Fact]
    public void Calculate_AllCorrectWithEqualWeight_ReturnsMaxScore()
    {
        var results = new[]
        {
            CreateResult(true, 1),
            CreateResult(true, 1)
        };

        var result = _sut.Calculate(results);

        result.ShouldBe(100);
    }

    [Fact]
    public void Calculate_AllIncorrect_ReturnsZero()
    {
        var results = new[]
        {
            CreateResult(false, 1),
            CreateResult(false, 2)
        };

        var result = _sut.Calculate(results);

        result.ShouldBe(0);
    }

    [Fact]
    public void Calculate_PartiallyCorrect_ReturnsNormalizedScore()
    {
        var results = new[]
        {
            CreateResult(true, 2),
            CreateResult(false, 1)
        };

        // base = 50
        // Q1 = 50 + 25 = 75
        // Q2 = 50
        // total = 125
        // earned = 75
        // 75 / 125 * 100 = 60
        var result = _sut.Calculate(results);

        result.ShouldBe(60);
    }

    [Fact]
    public void Calculate_ZeroTotalPossible_ReturnsZero()
    {
        var sut = new SuccessBonusScoreCalculator(bonusFactor: -1);

        var results = new[]
        {
            CreateResult(true, 2)
        };

        var result = sut.Calculate(results);

        result.ShouldBe(0);
    }

    private static ResultResponseDto CreateResult(bool isCorrect, int weight)
        => new()
        {
            Id = Guid.NewGuid(),
            AttemptId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            IsCorrect = isCorrect,
            Score = isCorrect ? 100 : 0,
            Weight = weight,
            AnsweredAt = DateTime.UtcNow
        };
}