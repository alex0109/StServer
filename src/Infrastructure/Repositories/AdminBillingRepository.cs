using System.Text.Json;
using Application.DTOs.Admin;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Subscription;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class AdminBillingRepository : IAdminBillingRepository
{
    private readonly AppDbContext _db;

    public AdminBillingRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<UserSubscription>> ListSubscriptionsAsync() =>
        _db.UserSubscriptions.AsNoTracking().ToListAsync();

    public Task<List<Guid>> ListDuoPayersWithMemberAsync() =>
        _db.DuoMemberships.Where(x => x.MemberUserId != null).Select(x => x.UserId).ToListAsync();

    public async Task<List<SubscriptionEventRow>> ListSubscriptionEventsAsync(DateTime since)
    {
        // Only a few subscription webhooks a month; the fields the statistics need are in the payload
        var rows = await _db.SubscriptionEvents.AsNoTracking()
            .Where(x => x.Provider != SubscriptionProvider.Manual && x.ReceivedAt >= since)
            .Select(x => new { x.Type, x.UserId, x.ReceivedAt, x.Payload })
            .ToListAsync();

        return rows
            .Select(x =>
            {
                var root = x.Payload.RootElement;
                var subscriptionId = root.TryGetProperty("subscriptionId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
                var occurredAt = root.TryGetProperty("occurredAt", out var at) && at.TryGetDateTime(out var time)
                    ? DateTime.SpecifyKind(time.ToUniversalTime(), DateTimeKind.Utc)
                    : x.ReceivedAt;
                var canceling = root.TryGetProperty("cancelAtPeriodEnd", out var cancel) && cancel.ValueKind == JsonValueKind.True;
                x.Payload.Dispose();
                return new SubscriptionEventRow(subscriptionId, x.UserId, x.Type, occurredAt, canceling);
            })
            .OrderBy(x => x.OccurredAt)
            .ToList();
    }
}
