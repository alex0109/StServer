using StServer.Domain.Entities;
using StServer.Application.DTOs.Result;

namespace StServer.Application.Mappers;

public class ResultMapper
{
    public static Result ToEntity(ResultCreateDto dto, bool isCorrect)
    {
        return new Result
        {
            Id = Guid.NewGuid(),
            AssessmentId =  dto.AssessmentId,
            QuestionId = dto.QuestionId,
            UserAnswer =  dto.UserAnswer,
            IsCorrect =  isCorrect,
            AnsweredAt = DateTime.UtcNow,
        };
    }

    public static ResultResponseDto ToDto(Result entity)
    {
        return new ResultResponseDto
        {
            Id = entity.Id,
            AssessmentId =  entity.AssessmentId,
            QuestionId = entity.QuestionId,
            UserAnswer =  entity.UserAnswer,
            IsCorrect =  entity.IsCorrect,
            AnsweredAt = entity.AnsweredAt,
        };
    }
    
    public static void ApplyUpdate(Result entity, ResultUpdateDto dto)
    {
        if (dto.UserAnswer is not null)
        {
            entity.UserAnswer = dto.UserAnswer;
        }
        
        if (dto.IsCorrect is bool isCorrect)
        {
            entity.IsCorrect = isCorrect;
        }
    }
}