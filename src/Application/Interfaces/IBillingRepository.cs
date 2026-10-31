using Domain.Entities;
using Domain.Utility.Subscription;

namespace Application.Interfaces;

public interface IBillingRepository
{
    Task<UserSubscription?> GetSubscriptionAsync(Guid userId);
    Task<UserSubscription?> FindByProviderSubscriptionAsync(SubscriptionProvider provider, string subscriptionId);
    Task AddSubscriptionAsync(UserSubscription subscription);
    void RemoveSubscription(UserSubscription subscription);
    Task<List<UserSubscription>> ListSubscriptionsAsync(int take);

    // False when the same provider event is already stored
    Task<bool> TryAddEventAsync(SubscriptionEvent subscriptionEvent);
    Task<SubscriptionEvent?> GetEventAsync(Guid eventId);

    Task SaveChangesAsync();
}
