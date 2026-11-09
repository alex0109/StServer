using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Subscription;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Repositories;

public class BillingRepository : IBillingRepository
{
    private readonly AppDbContext _db;

    public BillingRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<UserSubscription?> GetSubscriptionAsync(Guid userId) =>
        _db.UserSubscriptions.FirstOrDefaultAsync(x => x.UserId == userId);

    public Task<UserSubscription?> FindByProviderSubscriptionAsync(SubscriptionProvider provider, string subscriptionId) =>
        _db.UserSubscriptions.FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderSubscriptionId == subscriptionId);

    public async Task AddSubscriptionAsync(UserSubscription subscription)
    {
        await _db.UserSubscriptions.AddAsync(subscription);
    }

    public void RemoveSubscription(UserSubscription subscription)
    {
        _db.UserSubscriptions.Remove(subscription);
    }

    public Task<List<UserSubscription>> ListSubscriptionsAsync(int take) =>
        _db.UserSubscriptions.AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Take(take)
            .ToListAsync();

    public Task<DuoMembership?> GetDuoAsync(Guid payerUserId) =>
        _db.DuoMemberships.FirstOrDefaultAsync(x => x.UserId == payerUserId);

    public Task<DuoMembership?> FindDuoByMemberAsync(Guid memberUserId) =>
        _db.DuoMemberships.FirstOrDefaultAsync(x => x.MemberUserId == memberUserId);

    public Task<DuoMembership?> FindDuoByInviteAsync(string tokenHash) =>
        _db.DuoMemberships.FirstOrDefaultAsync(x => x.InviteTokenHash == tokenHash);

    public async Task AddDuoAsync(DuoMembership duo)
    {
        await _db.DuoMemberships.AddAsync(duo);
    }

    public async Task<bool> TryAddEventAsync(SubscriptionEvent subscriptionEvent)
    {
        // The unique index on the provider's event id is the guard: two parallel deliveries
        // can't both pass a check made before the insert
        _db.SubscriptionEvents.Add(subscriptionEvent);
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _db.Entry(subscriptionEvent).State = EntityState.Detached;
            return false;
        }
    }

    public Task<SubscriptionEvent?> GetEventAsync(Guid eventId) =>
        _db.SubscriptionEvents.FirstOrDefaultAsync(x => x.Id == eventId);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
