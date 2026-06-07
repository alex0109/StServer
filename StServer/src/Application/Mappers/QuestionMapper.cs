using StServer.Domain.Entities;
using StServer.Application.DTOs.Question;

namespace StServer.Application.Mappers;

public static class QuestionMapper
{
    public static Question ToEntity(QuestionCreateDto dto)
    {
        return new Question
        {
            Id = Guid.NewGuid(),
            MaterialId = dto.MaterialId,
            Title = dto.Title,
            Answer = dto.Answer,
            Difficulty = dto.Difficulty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static QuestionResponseDto ToDto(Question entity)
    {
        return new QuestionResponseDto
        {
            Id = entity.Id,
            MaterialId = entity.MaterialId,
            Title = entity.Title,
            Answer = entity.Answer,
            Difficulty =  entity.Difficulty,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
    
    public static void ApplyUpdate(Question entity, QuestionUpdateDto dto)
    {
        if (dto.Title is not null)
            entity.Title = dto.Title;

        if (dto.Answer is not null)
            entity.Answer = dto.Answer;

        if (dto.Difficulty is int difficulty)
            entity.Difficulty = difficulty;

        entity.UpdatedAt = DateTime.UtcNow;
    }
}