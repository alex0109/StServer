using Application.Billing;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Subscription;

namespace Application.Services;

public class SubscriptionTierProvider : ISubscriptionTierProvider
{
    private readonly IAiUsageRepository _subscriptions;
    private readonly TimeProvider _time;
    private readonly Dictionary<Guid, (SubscriptionTier Tier, UserSubscription? Source)> _cache = new();

    public SubscriptionTierProvider(IAiUsageRepository subscriptions, TimeProvider time)
    {
        _subscriptions = subscriptions;
        _time = time;
    }

    // Read once per request: one create can check several limits
    public async Task<SubscriptionTier> GetTierAsync(Guid userId) => (await ResolveAsync(userId)).Tier;

    public async Task<AiPlanPeriod> GetAiPlanAsync(Guid userId)
    {
        var (tier, source) = await ResolveAsync(userId);
        var now = _time.GetUtcNow().UtcDateTime;

        // A plan paid through a provider counts in its billing period; one given by hand, Free, or a
        // paid plan in its grace days after the period counts in calendar months
        if (tier != SubscriptionTier.Free
            && source is { Provider: not SubscriptionProvider.Manual, CurrentPeriodStart: { } start, CurrentPeriodEnd: { } end }
            && start <= now && now < end)
        {
            return new AiPlanPeriod(tier, start, end);
        }

        var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new AiPlanPeriod(tier, month, month.AddMonths(1));
    }

    private async Task<(SubscriptionTier Tier, UserSubscription? Source)> ResolveAsync(Guid userId)
    {
        if (_cache.TryGetValue(userId, out var cached)) return cached;

        var subscription = await _subscriptions.GetSubscriptionAsync(userId);
        var now = _time.GetUtcNow().UtcDateTime;

        var tier = SubscriptionRules.EffectiveTier(subscription, now);
        var source = tier == SubscriptionTier.Free ? null : subscription;
        if (tier == SubscriptionTier.Free)
        {
            // On someone's Duo: the payer's plan and its period
            var payer = await _subscriptions.GetDuoPayerSubscriptionAsync(userId);
            tier = SubscriptionRules.EffectiveTier(subscription, payer, now);
            source = tier == SubscriptionTier.Free ? null : payer;
        }

        _cache[userId] = (tier, source);
        return (tier, source);
    }
}
