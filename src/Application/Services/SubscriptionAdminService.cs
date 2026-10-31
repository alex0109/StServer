using Application.Billing;
using Application.DTOs.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Admin;
using Domain.Utility.Subscription;

namespace Application.Services;

// Plans given by hand, e.g. Plus for beta testers. Every change is audited and emailed to the owner
public class SubscriptionAdminService : ISubscriptionAdminService
{
    private const int MaxReasonLength = 300;

    private readonly IBillingRepository _repo;
    private readonly IAdminAudit _audit;
    private readonly IAdminAlerts _alerts;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public SubscriptionAdminService(
        IBillingRepository repo,
        IAdminAudit audit,
        IAdminAlerts alerts,
        IUserContext user,
        TimeProvider time)
    {
        _repo = repo;
        _audit = audit;
        _alerts = alerts;
        _user = user;
        _time = time;
    }

    public async Task<List<AdminSubscriptionDto>> ListAsync(int take)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        return (await _repo.ListSubscriptionsAsync(take)).Select(x => ToDto(x, now)).ToList();
    }

    public async Task<AdminSubscriptionDto> GrantAsync(SubscriptionGrantDto dto)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var reason = dto.Reason?.ReplaceLineEndings(" ").Trim() ?? "";

        if (dto.UserId == Guid.Empty) throw new BadRequestException("Enter the user id");
        if (dto.Tier == SubscriptionTier.Free) throw new BadRequestException("Free needs no grant, revoke instead");
        if (dto.Until is { } until && until <= now) throw new BadRequestException("The end date must be in the future");
        if (reason.Length is < 3 or > MaxReasonLength)
            throw new BadRequestException($"Write a reason, 3 to {MaxReasonLength} characters");

        var subscription = await _repo.GetSubscriptionAsync(dto.UserId);

        // A paying user's plan belongs to the provider; changing it here would be undone by the next webhook
        if (subscription is not null
            && subscription.Provider != SubscriptionProvider.Manual
            && SubscriptionRules.EffectiveTier(subscription, now) != SubscriptionTier.Free)
        {
            throw new ConflictException("This user pays through a provider");
        }

        if (subscription is null)
        {
            subscription = new UserSubscription { UserId = dto.UserId, CreatedAt = now };
            await _repo.AddSubscriptionAsync(subscription);
        }

        subscription.Tier = dto.Tier;
        subscription.Status = SubscriptionStatus.Active;
        subscription.Provider = SubscriptionProvider.Manual;
        subscription.ProviderCustomerId = null;
        subscription.ProviderSubscriptionId = null;
        subscription.CurrentPeriodStart = now;
        subscription.CurrentPeriodEnd = dto.Until is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null;
        subscription.TrialEndsAt = null;
        subscription.CancelAtPeriodEnd = false;
        subscription.GrantedByAdminId = _user.UserId;
        subscription.GrantReason = reason;
        subscription.UpdatedAt = now;

        await _repo.SaveChangesAsync();

        var period = subscription.CurrentPeriodEnd is { } date ? $"до {date:yyyy-MM-dd}" : "без строку";
        await _audit.RecordAsync(AdminAction.GrantSubscription, dto.UserId, $"{dto.Tier} {period}: {reason}");
        await _alerts.SubscriptionChangedAsync($"Видано {dto.Tier} {period} користувачу {dto.UserId}. Причина: {reason}");

        return ToDto(subscription, now);
    }

    public async Task<bool> RevokeAsync(Guid userId)
    {
        var subscription = await _repo.GetSubscriptionAsync(userId);
        if (subscription is null || subscription.Provider != SubscriptionProvider.Manual) return false;

        var tier = subscription.Tier;
        _repo.RemoveSubscription(subscription);
        await _repo.SaveChangesAsync();

        await _audit.RecordAsync(AdminAction.RevokeSubscription, userId, $"{tier} знято");
        await _alerts.SubscriptionChangedAsync($"Знято ручний {tier} у користувача {userId}");

        return true;
    }

    private static AdminSubscriptionDto ToDto(UserSubscription subscription, DateTime now) => new(
        subscription.UserId,
        subscription.Tier,
        SubscriptionRules.EffectiveTier(subscription, now),
        subscription.Status,
        subscription.Provider,
        subscription.ProviderCustomerId,
        subscription.CurrentPeriodEnd,
        subscription.CancelAtPeriodEnd,
        subscription.GrantReason,
        subscription.GrantedByAdminId,
        subscription.UpdatedAt);
}
