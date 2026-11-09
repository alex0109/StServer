using Domain.Entities;
using Domain.Utility.Ai;

namespace Application.Interfaces;

public interface IAiUsageRepository
{
    Task<UserSubscription?> GetSubscriptionAsync(Guid userId);

    // The subscription of whoever invited this user to their Duo, null when they are on none
    Task<UserSubscription?> GetDuoPayerSubscriptionAsync(Guid memberUserId);
    // Pending and succeeded requests count against the quota, failed ones do not
    Task<int> CountAsync(Guid userId, AiFeature feature, DateTime since);
    Task<Dictionary<AiFeature, int>> CountByFeatureAsync(Guid userId, DateTime since);
    Task AddAsync(AiUsage usage);
    Task<AiUsage?> GetByIdAsync(Guid usageId);
    Task<int> FailPendingAsync(DateTime createdBefore, DateTime now);
    // Single requests created before the date become monthly totals
    Task<int> RollUpBeforeAsync(DateTime createdBefore);
    Task SaveChangesAsync();
}
