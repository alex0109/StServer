using Domain.Entities;
using Application.DTOs.Result;

namespace Application.Mappers;

public class ResultMapper
{
    public static Result ToEntity(ResultRequestDto dto,
        Guid attemptId,
        Guid userId,
        bool isCorrect,
        double score,
        int weight,
        TimeSpan timeSpent)
    {
        return new Result
        {
            Id = Guid.NewGuid(),
            QuestionId = dto.QuestionId,
            AttemptId = attemptId,
            UserId = userId,
            UserAnswer = dto.UserAnswer ?? null,
            UserAnswerOptionId = dto.UserAnswerOptionId ?? null,
            IsCorrect = isCorrect,
            Score = score,
            Weight = weight,
            ConfidenceLevel = dto.ConfidenceLevel,
            TimeSpent = timeSpent,
            AnsweredAt = DateTime.UtcNow,
        };
    }

    public static ResultResponseDto ToDto(Result entity)
    {
        return new ResultResponseDto
        {
            Id = entity.Id,
            AttemptId = entity.AttemptId,
            QuestionId = entity.QuestionId,
            UserAnswer = entity.UserAnswer ?? null,
            UserAnswerOptionId = entity.UserAnswerOptionId ?? null,
            IsCorrect = entity.IsCorrect,
            Score = entity.Score,
            Weight = entity.Weight,
            ConfidenceLevel = entity.ConfidenceLevel,
            AnswerChangedCount = entity.AnswerChangedCount,
            TimeSpent = entity.TimeSpent,
            AnsweredAt = entity.AnsweredAt,
        };
    }
    
    public static void ApplyUpdate(Result entity, ResultRequestDto dto, bool isCorrect, double score)
    {
        entity.UserAnswer = dto.UserAnswer ?? null;
        entity.UserAnswerOptionId = dto.UserAnswerOptionId ?? null;
        entity.IsCorrect = isCorrect;
        entity.Score = score;
        entity.ConfidenceLevel = dto.ConfidenceLevel;
        entity.AnswerChangedCount++;
    }
}