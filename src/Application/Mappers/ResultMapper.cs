using Domain.Entities;
using Application.DTOs.Result;
using Domain.Utility.Question;

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
            UserAnswerOptionIds = dto.UserAnswerOptionIds?.Distinct().ToList() ?? [],
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
            UserAnswerOptionIds = entity.UserAnswerOptionIds,
            IsCorrect = entity.IsCorrect,
            Score = entity.Score,
            Weight = entity.Weight,
            ConfidenceLevel = entity.ConfidenceLevel,
            Rating = entity.Rating,
            Method = entity.Method,
            AiExplanation = entity.AiExplanation,
            CorrectOptionIds = entity.Question?.Options
                .Where(o => o.IsCorrect)
                .Select(o => o.Id)
                .ToList() ?? [],
            CorrectAnswer = entity.Question?.QuestionType == QuestionType.Open ? entity.Question.Answer : null,
            QuestionExplanation = entity.Question?.Explanation,
            AnswerChangedCount = entity.AnswerChangedCount,
            TimeSpent = entity.TimeSpent,
            AnsweredAt = entity.AnsweredAt,
        };
    }
    
    public static void ApplyUpdate(Result entity, ResultRequestDto dto, bool isCorrect, double score)
    {
        entity.UserAnswer = dto.UserAnswer ?? null;
        entity.UserAnswerOptionIds = dto.UserAnswerOptionIds?.Distinct().ToList() ?? [];
        entity.IsCorrect = isCorrect;
        entity.Score = score;
        entity.ConfidenceLevel = dto.ConfidenceLevel;
        // An explanation of the previous answer would be wrong for the new one
        entity.AiExplanation = null;
        entity.AnswerChangedCount++;
    }
}