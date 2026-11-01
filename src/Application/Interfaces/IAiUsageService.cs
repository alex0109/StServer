using Application.Ai;
using Application.DTOs.Ai;
using Domain.Utility.Ai;

namespace Application.Interfaces;

public interface IAiUsageService
{
    // Takes one request from the monthly quota or throws AiQuotaExceededException
    Task<Guid> ReserveAsync(Guid userId, AiFeature feature);
    Task CompleteAsync(Guid usageId, AiCompletion completion);
    // Gives the request back to the quota
    Task FailAsync(Guid usageId);
    // The AI answered with nothing usable: its tokens are kept for the cost report, the quota place is given back
    Task RefundAsync(Guid usageId, AiCompletion completion);
    Task<AiUsageResponseDto> GetUsageAsync(Guid userId);
    Task<int> FailStaleAsync();
}
