using Application.DTOs.Attempt;
using Application.DTOs.Result;
using Domain.Entities;
using Domain.Utility.Attempt;

namespace Application.Mappers;

public class AttemptMapper
{
    public static Attempt ToEntity(Guid assessmentId, Guid userId)
    {
        return new Attempt
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            UserId = userId,
            AttemptStatus = AttemptStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };
    }

    public static AttemptResponseDto ToDto(Attempt entity)
    {
        var results = entity.Results ?? [];;

        var correct = results.Count(x => x.IsCorrect);
        var wrong = results.Count(x => !x.IsCorrect);

        return new AttemptResponseDto
        {
            Id = entity.Id,
            AssessmentId = entity.AssessmentId,
            AttemptStatus = entity.AttemptStatus,
            Score = 0,
            CorrectAnswers = correct,
            WrongAnswers = wrong,
            TotalTimeSeconds = results.Sum(x => x.TimeSpent.TotalSeconds),
            Results = results
                .Select(ResultMapper.ToDto)
                .ToList(),
            StartedAt = entity.StartedAt,
            FinishedAt = entity.FinishedAt
        };
    }
}