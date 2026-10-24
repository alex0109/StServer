using Domain.Entities;
using Domain.Utility.Ai;

namespace Application.Interfaces;

public interface IAiUsageRepository
{
    Task<UserSubscription?> GetSubscriptionAsync(Guid userId);
    // Pending and succeeded requests count against the quota, failed ones do not
    Task<int> CountAsync(Guid userId, AiFeature feature, DateTime since);
    Task<Dictionary<AiFeature, int>> CountByFeatureAsync(Guid userId, DateTime since);
    Task AddAsync(AiUsage usage);
    Task<AiUsage?> GetByIdAsync(Guid usageId);
    Task<int> FailPendingAsync(DateTime createdBefore, DateTime now);
    Task SaveChangesAsync();
}
