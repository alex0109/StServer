using Application.Billing;
using Domain.Utility.Subscription;

namespace Application.Interfaces;

// The plan a user is on right now; the one place that decides it, for limits and AI quotas
public interface ISubscriptionTierProvider
{
    Task<SubscriptionTier> GetTierAsync(Guid userId);

    // The tier with the billing period its AI quotas count in: Stripe's current period for a
    // paid plan (a Duo member uses the payer's), the calendar month in UTC otherwise
    Task<AiPlanPeriod> GetAiPlanAsync(Guid userId);
}
