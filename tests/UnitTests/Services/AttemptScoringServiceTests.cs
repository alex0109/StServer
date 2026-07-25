using Application.DTOs.Attempt;
using Application.Interfaces;
using Application.Services;
using Application.DTOs.Result;
using Application.ScoreCalculators;
using Domain.Utility.Attempt;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class AttemptScoringServiceTests
{
    private readonly Mock<IAverageScoreCalculator> _averageCalculator = new();
    private readonly Mock<INonLinearDifficultyScoreCalculator> _nonLinearCalculator = new();
    private readonly Mock<ISuccessBonusScoreCalculator> _bonusCalculator = new();

    private readonly AttemptScoringService _sut;

    public AttemptScoringServiceTests()
    {
        _sut = new AttemptScoringService(
            _averageCalculator.Object,
            _nonLinearCalculator.Object,
            _bonusCalculator.Object);
    }
    
    [Fact]
    public void AverageScoreAttempt_ShouldSetAverageScore()
    {
        var attempt = CreateAttempt();

        _averageCalculator
            .Setup(x => x.Calculate(attempt.Results))
            .Returns(75);

        _sut.AverageScoreAttempt(attempt);


        attempt.Score.ShouldBe(75);

        _averageCalculator.Verify(
            x => x.Calculate(attempt.Results),
            Times.Once);

        _nonLinearCalculator.Verify(
            x => x.Calculate(It.IsAny<List<ResultResponseDto>>()),
            Times.Never);

        _bonusCalculator.Verify(
            x => x.Calculate(It.IsAny<List<ResultResponseDto>>()),
            Times.Never);
    }
    
    [Fact]
    public void NonLinearScoreAttempt_ShouldSetNonLinearScore()
    {
        var attempt = CreateAttempt();

        _nonLinearCalculator
            .Setup(x => x.Calculate(attempt.Results))
            .Returns(82.5);

        _sut.NonLinearScoreAttempt(attempt);
        
        attempt.Score.ShouldBe(82.5);

        _nonLinearCalculator.Verify(
            x => x.Calculate(attempt.Results),
            Times.Once);

        _averageCalculator.Verify(
            x => x.Calculate(It.IsAny<List<ResultResponseDto>>()),
            Times.Never);
    }

    [Fact]
    public void SuccessBonusScoreAttempt_ShouldSetBonusScore()
    {
        var attempt = CreateAttempt();

        _bonusCalculator
            .Setup(x => x.Calculate(attempt.Results))
            .Returns(95);
        
        _sut.SuccessBonusScoreAttempt(attempt);

        attempt.Score.ShouldBe(95);

        _bonusCalculator.Verify(
            x => x.Calculate(attempt.Results),
            Times.Once);

        _averageCalculator.Verify(
            x => x.Calculate(It.IsAny<List<ResultResponseDto>>()),
            Times.Never);

        _nonLinearCalculator.Verify(
            x => x.Calculate(It.IsAny<List<ResultResponseDto>>()),
            Times.Never);
    }
    
    [Fact]
    public void AverageScoreAttempt_WithEmptyResults_ShouldStillSetScore()
    {
        var attempt = new AttemptResponseDto
        {
            Results = [],
            StartedAt = DateTime.UtcNow
        };

        _averageCalculator
            .Setup(x => x.Calculate(attempt.Results))
            .Returns(0);
        
        _sut.AverageScoreAttempt(attempt);
        
        attempt.Score.ShouldBe(0);
    }

    [Fact]
    public void AverageScoreAttempt_ShouldPassSameResultsReference()
    {
        var attempt = CreateAttempt();

        _averageCalculator
            .Setup(x => x.Calculate(It.IsAny<List<ResultResponseDto>>()))
            .Returns(50);
        
        _sut.AverageScoreAttempt(attempt);
        
        _averageCalculator.Verify(
            x => x.Calculate(
                It.Is<List<ResultResponseDto>>(r =>
                    ReferenceEquals(r, attempt.Results))),
            Times.Once);
    }

    private static AttemptResponseDto CreateAttempt()
    {
        return new AttemptResponseDto
        {
            Id = Guid.NewGuid(),
            AssessmentId = Guid.NewGuid(),
            AttemptStatus = AttemptStatus.Finished,
            Results =
            [
                new ResultResponseDto
                {
                    Id = Guid.NewGuid(),
                    AttemptId = Guid.NewGuid(),
                    QuestionId = Guid.NewGuid(),
                    IsCorrect = true,
                    Score = 100,
                    Weight = 1,
                    AnsweredAt = DateTime.UtcNow
                }
            ],
            StartedAt =  DateTime.UtcNow
        };
    }
}