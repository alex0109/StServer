using StServer.Domain.Entities;
using StServer.Application.DTOs.Result;

namespace StServer.Application.Mappers;

public class ResultMapper
{
    public static Result ToEntity(ResultRequestDto dto, bool isCorrect)
    {
        return new Result
        {
            Id = Guid.NewGuid(),
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
            AttemptId =  entity.AttemptId,
            QuestionId = entity.QuestionId,
            UserAnswer =  entity.UserAnswer,
            IsCorrect =  entity.IsCorrect,
            AnsweredAt = entity.AnsweredAt,
        };
    }
}