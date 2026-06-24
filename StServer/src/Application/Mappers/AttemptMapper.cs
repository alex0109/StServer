using StServer.Application.DTOs.Attempt;
using StServer.Application.DTOs.Result;
using StServer.Domain.Entities;
using StServer.Domain.Utility.Attempt;

namespace StServer.Application.Mappers;

public class AttemptMapper
{
    public static Attempt ToEntity(Guid assessmentId)
    {
        return new Attempt
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            AttemptStatus = AttemptStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };
    }

    public static AttemptResponseDto ToDto(Attempt entity)
    {
        var results = entity.Results ?? [];;

        var correct = results.Count(x => x.IsCorrect);
        var wrong = results.Count(x => !x.IsCorrect);
        var total = results.Count;

        return new AttemptResponseDto
        {
            Id = entity.Id,
            AssessmentId = entity.AssessmentId,
            AttemptStatus = entity.AttemptStatus,
            // TODO: LOGIC OF SCORE ESTIMATE
            Score = total == 0 ? 0 : correct * 100 / total,
            CorrectAnswers = correct,
            WrongAnswers = wrong,
            TotalTimeSeconds = results.Sum(x => x.TimeSpent.TotalSeconds),
            Results = entity.Results
                .Select(ResultMapper.ToDto)
                .ToList(),
            StartedAt = entity.StartedAt,
            FinishedAt = entity.FinishedAt
        };
    }
}