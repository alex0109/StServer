using Application.DTOs.Option;
using Domain.Entities;
using Application.DTOs.Question;
using Domain.Utility.Question;

namespace Application.Mappers;

public static class QuestionMapper
{
    public static Question ToEntityOpenQuestion(OpenQuestionCreateDto dto, Guid materialId, Guid userId)
    {
        return new Question
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            UserId = userId,
            Title = dto.Title,
            Answer = dto.Answer,
            QuestionDifficulty =  dto.QuestionDifficulty,
            QuestionType = QuestionType.Open,
            Explanation = dto?.Explanation,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
    
    public static Question ToEntityOptionsQuestion(OptionQuestionCreateDto dto, Guid materialId, Guid userId)
    {
        return new Question
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            UserId = userId,
            Title = dto.Title,
            QuestionType = QuestionType.Options,
            QuestionDifficulty =  dto.QuestionDifficulty,
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
            QuestionType = entity.QuestionType,
            Explanation = entity.Explanation,
            QuestionDifficulty = entity.QuestionDifficulty,
            Options = entity.Options.Select(o => new OptionResponseDto
            {
                Id = o.Id,
                Name = o.Name,
                IsCorrect = o.Id == entity.CorrectOptionId
            }).ToList(),
            IsActive = entity.IsActive,
            Version = entity.Version,
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
            QuestionType = entity.QuestionType,
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
        
        if (dto.CorrectOptionId is not null)
            entity.CorrectOptionId = dto.CorrectOptionId;
        
        if (dto.QuestionDifficulty is QuestionDifficulty diff)
            entity.QuestionDifficulty = diff;
        
        if (dto.Explanation is not null)
            entity.Explanation = dto.Explanation;

        entity.Version++; 
        entity.UpdatedAt = DateTime.UtcNow;
    }
}