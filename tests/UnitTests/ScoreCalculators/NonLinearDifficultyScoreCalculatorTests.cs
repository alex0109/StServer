using Application.DTOs.Result;
using Application.ScoreCalculators;
using Domain.Utility.Question;
using Moq;
using Shouldly;

namespace UnitTests.ScoreCalculators;

public class NonLinearDifficultyScoreCalculatorTests
{
    private readonly Mock<IDifficultyWeightProvider> _weightProviderMock = new();
    private readonly NonLinearDifficultyScoreCalculator _sut;

    public NonLinearDifficultyScoreCalculatorTests()
    {
        _sut = new NonLinearDifficultyScoreCalculator(_weightProviderMock.Object);
    }

    [Fact]
    public void Calculate_EmptyResults_ReturnsZero()
    {
        var result = _sut.Calculate(Enumerable.Empty<ResultResponseDto>());

        result.ShouldBe(0);
    }

    [Fact]
    public void Calculate_SingleResult_ReturnsScaledScore()
    {
        _weightProviderMock
            .Setup(x => x.GetWeight(QuestionDifficulty.Easy))
            .Returns(1);

        var results = new[]
        {
            CreateResult(score: 80, difficulty: QuestionDifficulty.Easy)
        };

        var result = _sut.Calculate(results);

        result.ShouldBe(160);
    }

    [Fact]
    public void Calculate_MultipleResults_ReturnsWeightedScore()
    {
        _weightProviderMock
            .Setup(x => x.GetWeight(QuestionDifficulty.Easy))
            .Returns(1);

        _weightProviderMock
            .Setup(x => x.GetWeight(QuestionDifficulty.Hard))
            .Returns(3);

        var results = new[]
        {
            CreateResult(score: 100, difficulty: QuestionDifficulty.Easy),
            CreateResult(score: 50, difficulty: QuestionDifficulty.Hard)
        };
        
        var result = _sut.Calculate(results);

        result.ShouldBe(125);
    }

    [Fact]
    public void Calculate_ZeroTotalWeight_ReturnsZero()
    {
        _weightProviderMock
            .Setup(x => x.GetWeight(It.IsAny<QuestionDifficulty>()))
            .Returns(0);

        var results = new[]
        {
            CreateResult(score: 100, difficulty: QuestionDifficulty.Easy)
        };

        var result = _sut.Calculate(results);

        result.ShouldBe(0);
    }

    [Fact]
    public void Calculate_ResultExceedsMaxScore_ReturnsMaxScore()
    {
        _weightProviderMock
            .Setup(x => x.GetWeight(It.IsAny<QuestionDifficulty>()))
            .Returns(1);

        var results = new[]
        {
            CreateResult(score: 150, difficulty: QuestionDifficulty.Easy)
        };

        var result = _sut.Calculate(results);

        result.ShouldBe(200);
    }

    private static ResultResponseDto CreateResult(
        double score,
        QuestionDifficulty difficulty)
        => new()
        {
            Id = Guid.NewGuid(),
            AttemptId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            IsCorrect = score > 0,
            Score = score,
            Weight = (int)difficulty,
            AnsweredAt = DateTime.UtcNow
        };
}