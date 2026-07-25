using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Attempt;
using Shouldly;

namespace UnitTests.Mappers;

public class AttemptMapperTests
{
    [Fact]
    public void ToEntity_ShouldGenerateNewId()
    {
        var result = AttemptMapper.ToEntity(
            Guid.NewGuid(),
            Guid.NewGuid());

        result.Id.ShouldNotBe(Guid.Empty);
    }


    [Fact]
    public void ToEntity_ShouldMapForeignKeys()
    {
        var assessmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = AttemptMapper.ToEntity(
            assessmentId,
            userId);

        result.AssessmentId.ShouldBe(assessmentId);
        result.UserId.ShouldBe(userId);
    }


    [Fact]
    public void ToEntity_ShouldSetInitialStatus()
    {
        var result = AttemptMapper.ToEntity(
            Guid.NewGuid(),
            Guid.NewGuid());

        result.AttemptStatus.ShouldBe(
            AttemptStatus.InProgress);
    }


    [Fact]
    public void ToEntity_ShouldInitializeEmptyResults()
    {
        var result = AttemptMapper.ToEntity(
            Guid.NewGuid(),
            Guid.NewGuid());

        result.Results.ShouldNotBeNull();
        result.Results.ShouldBeEmpty();
    }


    [Fact]
    public void ToEntity_ShouldSetStartedAt()
    {
        var before = DateTime.UtcNow;

        var result = AttemptMapper.ToEntity(
            Guid.NewGuid(),
            Guid.NewGuid());

        var after = DateTime.UtcNow;

        result.StartedAt.ShouldBeInRange(
            before,
            after);
    }


    [Fact]
    public void ToDto_ShouldMapBasicProperties()
    {
        var attempt = CreateAttempt();

        var result = AttemptMapper.ToDto(attempt);

        result.Id.ShouldBe(attempt.Id);
        result.AssessmentId.ShouldBe(attempt.AssessmentId);
        result.AttemptStatus.ShouldBe(attempt.AttemptStatus);
        result.StartedAt.ShouldBe(attempt.StartedAt);
        result.FinishedAt.ShouldBe(attempt.FinishedAt);
    }


    [Fact]
    public void ToDto_WhenFinishedAtIsNull_ShouldRemainNull()
    {
        var attempt = CreateAttempt();

        attempt.FinishedAt = null;

        var result = AttemptMapper.ToDto(attempt);

        result.FinishedAt.ShouldBeNull();
    }


    [Fact]
    public void ToDto_WithAllCorrectAnswers_ShouldCalculateCorrectCount()
    {
        var attempt = CreateAttempt();

        attempt.Results =
        [
            CreateResult(true),
            CreateResult(true),
            CreateResult(true)
        ];

        var result = AttemptMapper.ToDto(attempt);

        result.CorrectAnswers.ShouldBe(3);
        result.WrongAnswers.ShouldBe(0);
    }


    [Fact]
    public void ToDto_WithAllWrongAnswers_ShouldCalculateWrongCount()
    {
        var attempt = CreateAttempt();

        attempt.Results =
        [
            CreateResult(false),
            CreateResult(false)
        ];

        var result = AttemptMapper.ToDto(attempt);

        result.CorrectAnswers.ShouldBe(0);
        result.WrongAnswers.ShouldBe(2);
    }


    [Fact]
    public void ToDto_WithMixedResults_ShouldCalculateStatistics()
    {
        var attempt = CreateAttempt();

        attempt.Results =
        [
            CreateResult(true, 5),
            CreateResult(false, 10),
            CreateResult(true, 15)
        ];

        var result = AttemptMapper.ToDto(attempt);

        result.CorrectAnswers.ShouldBe(2);
        result.WrongAnswers.ShouldBe(1);
        result.TotalTimeSeconds.ShouldBe(30);
    }


    [Fact]
    public void ToDto_ShouldMapResultsToDto()
    {
        var resultId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        var attempt = CreateAttempt();

        attempt.Results =
        [
            new Result
            {
                Id = resultId,
                QuestionId = questionId,
                AttemptId = attempt.Id,
                IsCorrect = true,
                Score = 100,
                Weight = 2,
                TimeSpent = TimeSpan.FromSeconds(5),
                AnsweredAt = DateTime.UtcNow
            }
        ];


        var dto = AttemptMapper.ToDto(attempt);


        dto.Results.Count.ShouldBe(1);

        dto.Results[0].Id.ShouldBe(resultId);
        dto.Results[0].QuestionId.ShouldBe(questionId);
        dto.Results[0].Score.ShouldBe(100);
        dto.Results[0].Weight.ShouldBe(2);
    }


    [Fact]
    public void ToDto_WithEmptyResults_ShouldReturnZeroStatistics()
    {
        var attempt = CreateAttempt();

        var result = AttemptMapper.ToDto(attempt);

        result.Results.ShouldBeEmpty();
        result.CorrectAnswers.ShouldBe(0);
        result.WrongAnswers.ShouldBe(0);
        result.TotalTimeSeconds.ShouldBe(0);
    }


    [Fact]
    public void ToDto_WithNullResults_ShouldHandleGracefully()
    {
        var attempt = CreateAttempt();

        attempt.Results = null!;

        var result = AttemptMapper.ToDto(attempt);

        result.Results.ShouldBeEmpty();
        result.CorrectAnswers.ShouldBe(0);
        result.WrongAnswers.ShouldBe(0);
        result.TotalTimeSeconds.ShouldBe(0);
    }


    [Fact]
    public void ToDto_ShouldNotCalculateScore()
    {
        var attempt = CreateAttempt();

        attempt.Results =
        [
            CreateResult(true, 100)
        ];

        var result = AttemptMapper.ToDto(attempt);

        result.Score.ShouldBe(0);
    }


    private static Attempt CreateAttempt()
    {
        return new Attempt
        {
            Id = Guid.NewGuid(),
            AssessmentId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AttemptStatus = AttemptStatus.Finished,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            FinishedAt = DateTime.UtcNow
        };
    }


    private static Result CreateResult(
        bool isCorrect,
        double seconds = 1)
    {
        return new Result
        {
            Id = Guid.NewGuid(),
            AttemptId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            IsCorrect = isCorrect,
            Score = isCorrect ? 100 : 0,
            Weight = 1,
            TimeSpent = TimeSpan.FromSeconds(seconds),
            AnsweredAt = DateTime.UtcNow
        };
    }
}