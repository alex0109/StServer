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

    // The Duo a payer invites to, the one a user is a member of, the one an invite link opens
    Task<DuoMembership?> GetDuoAsync(Guid payerUserId);
    Task<DuoMembership?> FindDuoByMemberAsync(Guid memberUserId);
    Task<DuoMembership?> FindDuoByInviteAsync(string tokenHash);
    Task AddDuoAsync(DuoMembership duo);

    // False when the same provider event is already stored
    Task<bool> TryAddEventAsync(SubscriptionEvent subscriptionEvent);
    Task<SubscriptionEvent?> GetEventAsync(Guid eventId);

    Task SaveChangesAsync();
}
