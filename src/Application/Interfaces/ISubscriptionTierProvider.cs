using Domain.Utility.Subscription;

namespace Application.Interfaces;

// The plan a user is on right now; the one place that decides it, for limits and AI quotas
public interface ISubscriptionTierProvider
{
    Task<SubscriptionTier> GetTierAsync(Guid userId);
}
