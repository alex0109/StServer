using StServer.Application.DTOs.Assessment;
using StServer.Domain.Entities;

namespace StServer.Application.Mappers;

public class AssessmentMapper
{
    public static Assessment ToEntity(Guid materialId)
    {
        return new Assessment
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
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