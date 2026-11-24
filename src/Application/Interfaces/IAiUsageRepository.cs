using Domain.Entities;
using Domain.Utility.Ai;

namespace Application.Interfaces;

public interface IAiUsageRepository
{
    Task<UserSubscription?> GetSubscriptionAsync(Guid userId);

    // The subscription of whoever invited this user to their Duo, null when they are on none
    Task<UserSubscription?> GetDuoPayerSubscriptionAsync(Guid memberUserId);
    // Units of pending and succeeded requests paid one way; failed ones don't count
    Task<int> SumUnitsAsync(Guid userId, AiFeature feature, AiUsageSource source, DateTime since);
    Task<Dictionary<AiFeature, int>> SumUnitsByFeatureAsync(Guid userId, AiUsageSource source, DateTime since);
    // Answers that came back unusable and were forgiven: failed requests a provider still answered
    Task<int> CountForgivenAsync(Guid userId, AiFeature feature, DateTime since);
    Task AddAsync(AiUsage usage);
    Task<AiUsage?> GetByIdAsync(Guid usageId);
    Task<List<Guid>> ListPendingAsync(DateTime createdBefore);
    // Single requests created before the date become monthly totals
    Task<int> RollUpBeforeAsync(DateTime createdBefore);
    Task SaveChangesAsync();
}
