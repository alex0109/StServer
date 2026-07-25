using Shouldly;
using Application.DTOs.Result;
using Application.ScoreCalculators;
using Xunit;

namespace StServer.UnitTests.ScoreCalculators;

public class AverageScoreCalculatorTests
{
    private readonly AverageScoreCalculator _sut = new(); // system under test

    [Fact]
    public void Calculate_EmptyResults_ReturnsZero()
    {
        var result = _sut.Calculate(Enumerable.Empty<ResultResponseDto>());

        result.ShouldBe(0);
    }

    [Fact]
    public void Calculate_SingleResult_ReturnsWeightedScore()
    {
        var results = new[]
        {
            CreateResult(score: 80, weight: 1)
        };

        var result = _sut.Calculate(results);

        result.ShouldBe(80);
    }

    [Fact]
    public void Calculate_MultipleResults_ReturnsWeightedAverage()
    {
        var results = new[]
        {
            CreateResult(score: 100, weight: 2), // враховується двічі
            CreateResult(score: 0, weight: 1)
        };

        // (100*2 + 0*1) / 3 = 66.67
        var result = _sut.Calculate(results);

        result.ShouldBe(66.67);
    }

    [Fact]
    public void Calculate_ZeroTotalWeight_ReturnsZero()
    {
        var results = new[] { CreateResult(score: 50, weight: 0) };

        var result = _sut.Calculate(results);

        result.ShouldBe(0);
    }

    private static ResultResponseDto CreateResult(double score, int weight) => new()
    {
        Id = Guid.NewGuid(),
        AttemptId = Guid.NewGuid(),
        QuestionId = Guid.NewGuid(),
        IsCorrect = score > 0,
        Score = score,
        Weight = weight,
        AnsweredAt = DateTime.UtcNow
    };
}