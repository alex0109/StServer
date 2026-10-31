using Application.Billing;
using Application.Interfaces;
using Domain.Utility.Subscription;

namespace Application.Services;

public class SubscriptionTierProvider : ISubscriptionTierProvider
{
    private readonly IAiUsageRepository _subscriptions;
    private readonly TimeProvider _time;
    private readonly Dictionary<Guid, SubscriptionTier> _cache = new();

    public SubscriptionTierProvider(IAiUsageRepository subscriptions, TimeProvider time)
    {
        _subscriptions = subscriptions;
        _time = time;
    }

    // Read once per request: one create can check several limits
    public async Task<SubscriptionTier> GetTierAsync(Guid userId)
    {
        if (_cache.TryGetValue(userId, out var cached)) return cached;

        var subscription = await _subscriptions.GetSubscriptionAsync(userId);
        var now = _time.GetUtcNow().UtcDateTime;

        var tier = SubscriptionRules.EffectiveTier(subscription, now);

        _cache[userId] = tier;
        return tier;
    }
}
