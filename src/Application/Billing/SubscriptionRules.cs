using Domain.Entities;
using Domain.Utility.Subscription;

namespace Application.Billing;

// The one place that says which tier is in force. Limits, AI quotas and the client all ask here
public static class SubscriptionRules
{
    // After a failed payment the provider retries for a while; the plan stays meanwhile
    public static readonly TimeSpan PastDueGrace = TimeSpan.FromDays(7);

    public static SubscriptionTier EffectiveTier(UserSubscription? subscription, DateTime now)
    {
        if (subscription is null) return SubscriptionTier.Free;

        var paidUntil = subscription.CurrentPeriodEnd;

        var inForce = subscription.Status switch
        {
            SubscriptionStatus.Expired => false,
            SubscriptionStatus.Trialing => (subscription.TrialEndsAt ?? paidUntil) is not { } end || end > now,
            SubscriptionStatus.PastDue => paidUntil is not { } due || due + PastDueGrace > now,
            // Canceled keeps what was paid for, to the end of the period
            SubscriptionStatus.Canceled => paidUntil is { } last && last > now,
            _ => paidUntil is not { } until || until > now,
        };

        return inForce ? subscription.Tier : SubscriptionTier.Free;
    }
}
