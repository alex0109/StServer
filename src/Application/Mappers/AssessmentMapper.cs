using Application.DTOs.Assessment;
using Domain.Entities;

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
            MaterialId = entity.MaterialId
        };
    }
}