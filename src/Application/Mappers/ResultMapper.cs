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
        int weight)
    {
        return new Result
        {
            Id = Guid.NewGuid(),
            QuestionId = dto.QuestionId,
            AttemptId = attemptId,
            UserId = userId,
            UserAnswer =  dto.UserAnswer ?? null,
            UserAnswerOptionId =  dto.UserAnswerOptionId ?? null,
            IsCorrect =  isCorrect,
            Score = score,
            Weight = weight,
            AnsweredAt = DateTime.UtcNow,
        };
    }

    public static ResultResponseDto ToDto(Result entity)
    {
        return new ResultResponseDto
        {
            Id = entity.Id,
            AttemptId =  entity.AttemptId,
            QuestionId = entity.QuestionId,
            UserAnswer =  entity.UserAnswer ?? null,
            UserAnswerOptionId = entity.UserAnswerOptionId ?? null,
            IsCorrect =  entity.IsCorrect,
            Score = entity.Score,
            Weight = entity.Weight,
            AnsweredAt = entity.AnsweredAt,
        };
    }
}