using StServer.Application.DTOs.Assessment;
using StServer.Domain.Entities;

namespace StServer.Application.Mappers;

public class AssessmentMapper
{
    public static Assessment ToEntity(AssessmentCreateDto dto, int totalQuestions)
    {
        return new Assessment
        {
            Id = Guid.NewGuid(),
            MaterialId = dto.MaterialId,
            TotalQuestions = totalQuestions,
            CorrectAnswers = 0,
            Score = 0,
            StartedAt = DateTime.UtcNow
        };
    }

    public static AssessmentResponseDto ToDto(Assessment entity)
    {
        return new AssessmentResponseDto
        {
            Id = entity.Id,
            MaterialId = entity.MaterialId,
            TotalQuestions =  entity.TotalQuestions,
            CorrectAnswers = entity.CorrectAnswers,
            Score = entity.Score,
            StartedAt = entity.StartedAt,
            FinishedAt = entity.FinishedAt,
        };
    }
    
    public static void ApplyUpdate(Assessment entity, AssessmentUpdateDto dto)
    {
        if (dto.TotalQuestions is not null)
        {
            entity.TotalQuestions = dto.TotalQuestions;
        }

        if (dto.CorrectAnswers is not null)
        {
            entity.CorrectAnswers = dto.CorrectAnswers;
        }

        if (dto.Score is not null)
        {
            entity.Score = dto.Score;
        }
        
        entity.FinishedAt = DateTime.UtcNow;
    }
}