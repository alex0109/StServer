using StServer.StServer.Domain.Entities;
using StServer.StServer.Application.DTOs;

namespace StServer.StServer.Application.Mappers;

public static class AssessmentMapper
{
    public static Assessment ToEntity(AssessmentItemDto dto)
    {
        return new Assessment
        {
            Id = dto.Id,
            MaterialId = dto.MaterialId,
            Title = dto.Title,
            Answer = dto.Answer,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }

    public static AssessmentItemDto ToDto(Assessment entity)
    {
        return new AssessmentItemDto
        {
            Id = entity.Id,
            MaterialId = entity.MaterialId,
            Title = entity.Title,
            Answer = entity.Answer,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}