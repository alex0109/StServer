using Application.DTOs.Assessment;
using Domain.Entities;
using Domain.Utility.Question;

namespace Application.Mappers;

public class AssessmentMapper
{
    public static Assessment ToEntity(Guid materialId, Guid userId)
    {
        return new Assessment
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            UserId = userId
        };
    }

    public static AssessmentResponseDto ToDto(Assessment entity)
    {
        return new AssessmentResponseDto
        {
            Id = entity.Id,
            MaterialId = entity.MaterialId,
            TimerMinutes = entity.TimerMinutes,
            Difficulties = entity.Difficulties.Count == 0 ? [.. Enum.GetValues<QuestionDifficulty>()] : [.. entity.Difficulties],
            QuestionCount = entity.QuestionCount
        };
    }
}
