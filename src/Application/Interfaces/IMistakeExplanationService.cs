using Application.DTOs.Ai;

namespace Application.Interfaces;

public interface IMistakeExplanationService
{
    Task<List<ResultExplanationDto>?> ExplainAsync(Guid attemptId, CancellationToken cancellationToken = default);
}
