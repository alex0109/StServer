using Application.DTOs.Result;
using Application.Mappers;
using Shouldly;

namespace UnitTests.Mappers;

public class ResultMapperTests
{
    [Fact]
    public void ToEntity_ShouldMapProperties()
    {
        var questionId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var optionId = Guid.NewGuid();

        var dto = new ResultRequestDto
        {
            QuestionId = questionId,
            UserAnswer = "Answer",
            UserAnswerOptionId = optionId
        };

        var result = ResultMapper.ToEntity(
            dto,
            attemptId,
            userId,
            isCorrect: true,
            score: 95,
            weight: 3);

        result.ShouldNotBeNull();

        result.Id.ShouldNotBe(Guid.Empty);
        result.QuestionId.ShouldBe(questionId);
        result.AttemptId.ShouldBe(attemptId);
        result.UserId.ShouldBe(userId);

        result.UserAnswer.ShouldBe("Answer");
        result.UserAnswerOptionId.ShouldBe(optionId);

        result.IsCorrect.ShouldBeTrue();
        result.Score.ShouldBe(95);
        result.Weight.ShouldBe(3);

        result.AnsweredAt.ShouldBeInRange(
            DateTime.UtcNow.AddSeconds(-2),
            DateTime.UtcNow);
    }


    [Fact]
    public void ToEntity_WithNullOptionalFields_ShouldKeepNull()
    {
        var dto = new ResultRequestDto
        {
            QuestionId = Guid.NewGuid()
        };

        var result = ResultMapper.ToEntity(
            dto,
            Guid.NewGuid(),
            Guid.NewGuid(),
            isCorrect: false,
            score: 0,
            weight: 1);

        result.UserAnswer.ShouldBeNull();
        result.UserAnswerOptionId.ShouldBeNull();
    }


    [Fact]
    public void ToDto_ShouldMapProperties()
    {
        var entity = new Domain.Entities.Result
        {
            Id = Guid.NewGuid(),
            AttemptId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            UserAnswer = "My answer",
            UserAnswerOptionId = Guid.NewGuid(),
            IsCorrect = true,
            Score = 100,
            Weight = 2,
            AnsweredAt = DateTime.UtcNow
        };

        var result = ResultMapper.ToDto(entity);

        result.ShouldNotBeNull();

        result.Id.ShouldBe(entity.Id);
        result.AttemptId.ShouldBe(entity.AttemptId);
        result.QuestionId.ShouldBe(entity.QuestionId);

        result.UserAnswer.ShouldBe(entity.UserAnswer);
        result.UserAnswerOptionId.ShouldBe(entity.UserAnswerOptionId);

        result.IsCorrect.ShouldBeTrue();
        result.Score.ShouldBe(100);
        result.Weight.ShouldBe(2);

        result.AnsweredAt.ShouldBe(entity.AnsweredAt);
    }


    [Fact]
    public void ToDto_WithNullOptionalFields_ShouldKeepNull()
    {
        var entity = new Domain.Entities.Result
        {
            Id = Guid.NewGuid(),
            AttemptId = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            UserAnswer = null,
            UserAnswerOptionId = null,
            IsCorrect = false,
            Score = 0,
            Weight = 1,
            AnsweredAt = DateTime.UtcNow
        };

        var result = ResultMapper.ToDto(entity);

        result.UserAnswer.ShouldBeNull();
        result.UserAnswerOptionId.ShouldBeNull();
    }
}