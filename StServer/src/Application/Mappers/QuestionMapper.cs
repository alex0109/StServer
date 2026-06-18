using StServer.Application.DTOs.Option;
using StServer.Domain.Entities;
using StServer.Application.DTOs.Question;
using StServer.Domain.Utility.Question;

namespace StServer.Application.Mappers;

public static class QuestionMapper
{
    public static Question ToEntity(OpenQuestionCreateDto dto, Guid materialId)
    {
        return new Question
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            Title = dto.Title,
            Answer = dto.Answer,
            QuestionType = QuestionType.Open,
            Explanation = dto?.Explanation,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
    
    public static Question ToEntity(OptionQuestionCreateDto dto, Guid materialId)
    {
        return new Question
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            Title = dto.Title,
            QuestionType = QuestionType.Options,
            Explanation = dto?.Explanation,
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
            Options = entity.Options.Select(o => new OptionResponseDto
            {
                Id = o.Id,
                Name = o.Name,
                IsCorrect = o.Id == entity.CorrectOptionId
            }).ToList(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
    
    public static QuestionReducedDto ToReducedDto(Question entity)
    {
        return new QuestionReducedDto()
        {
            Id = entity.Id,
            Title = entity.Title,
            Options = entity.Options.Select(o => new OptionResponseDto
            {
                Id = o.Id,
                Name = o.Name,
                IsCorrect = o.Id == entity.CorrectOptionId
            }).ToList()
        };
    }
    
    public static void ApplyUpdate(Question entity, QuestionUpdateDto dto)
    {
        if (dto.Title is not null)
            entity.Title = dto.Title;

        if (dto.Answer is not null)
            entity.Answer = dto.Answer;
        
        if (dto.Explanation is not null)
            entity.Explanation = dto.Explanation;

        entity.UpdatedAt = DateTime.UtcNow;
    }
}