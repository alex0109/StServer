using StServer.Application.DTOs.Assessment;
using StServer.Domain.Entities;

namespace StServer.Application.Mappers;

public class AssessmentMapper
{
    public static Assessment ToEntity(Guid materialId, int totalQuestions)
    {
        return new Assessment
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
        };
    }

    public static AssessmentResponseDto ToDto(Assessment entity, 
        int totalAttempts, int averageScore, 
        int bestScore, DateTime lastAttemptAt)
    {
        return new AssessmentResponseDto
        {
            Id = entity.Id,
            MaterialId = entity.MaterialId,
            TotalAttempts =  totalAttempts,
            AverageScore = averageScore,
            BestScore = bestScore,
            LastAttemptAt = lastAttemptAt
        };
    }
}