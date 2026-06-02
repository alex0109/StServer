using StServer.Domain.Entities;
using StServer.Application.DTOs;

namespace StServer.Application.Mappers;

public static class QuestionMapper
{
    public static Question ToEntity(QuestionItemDto dto)
    {
        return new Question
        {
            Id = dto.Id,
            MaterialId = dto.MaterialId,
            Title = dto.Title,
            Answer = dto.Answer,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }

    public static QuestionItemDto ToDto(Question entity)
    {
        return new QuestionItemDto
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