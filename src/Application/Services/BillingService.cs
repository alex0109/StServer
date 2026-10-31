using System.Text.Json;
using Application.Billing;
using Application.DTOs.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Application.Observability;
using Domain.Entities;
using Domain.Utility.Subscription;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class BillingService : IBillingService
{
    private static readonly JsonSerializerOptions EventJson = new(JsonSerializerDefaults.Web);

    private readonly IEnumerable<IBillingProvider> _providers;
    private readonly IBillingRepository _repo;
    private readonly IBillingJobs _jobs;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        IEnumerable<IBillingProvider> providers,
        IBillingRepository repo,
        IBillingJobs jobs,
        IUserContext user,
        TimeProvider time,
        ILogger<BillingService> logger)
    {
        _providers = providers;
        _repo = repo;
        _jobs = jobs;
        _user = user;
        _time = time;
        _logger = logger;
    }

    public string? SignatureHeaderOf(string provider) => Find(provider)?.SignatureHeader;

    public async Task<WebhookOutcome> ReceiveWebhookAsync(string provider, string? signature, byte[] body)
    {
        var outcome = await ReceiveAsync(provider, signature, body);
        AppMetrics.BillingWebhook(outcome.ToString());
        return outcome;
    }

    private async Task<WebhookOutcome> ReceiveAsync(string provider, string? signature, byte[] body)
    {
        var billing = Find(provider);
        if (billing is null) return WebhookOutcome.UnknownProvider;

        var now = _time.GetUtcNow().UtcDateTime;
        if (!billing.VerifyWebhook(signature, body, now))
        {
            _logger.LogWarning("Billing webhook from {Provider} with a bad signature", billing.Name);
            return WebhookOutcome.BadSignature;
        }

        var parsed = billing.ParseEvent(body);
        if (parsed is null)
        {
            _logger.LogInformation("Billing webhook from {Provider} skipped: not an event the app uses", billing.Name);
            return WebhookOutcome.Ignored;
        }

        var stored = new SubscriptionEvent
        {
            Id = Guid.NewGuid(),
            Provider = billing.Provider,
            ProviderEventId = parsed.EventId,
            Type = parsed.Type.Length <= 100 ? parsed.Type : parsed.Type[..100],
            UserId = parsed.UserId,
            Payload = JsonSerializer.SerializeToDocument(parsed, EventJson),
            ReceivedAt = now,
        };

        // Providers deliver again until they get a 200: the second copy is answered and dropped
        if (!await _repo.TryAddEventAsync(stored)) return WebhookOutcome.Duplicate;

        _jobs.ProcessEvent(stored.Id);
        return WebhookOutcome.Accepted;
    }

    public async Task ProcessEventAsync(Guid eventId)
    {
        var stored = await _repo.GetEventAsync(eventId);
        if (stored is null || stored.ProcessedAt is not null) return;

        var now = _time.GetUtcNow().UtcDateTime;
        var data = stored.Payload.Deserialize<BillingEvent>(EventJson)!;

        var subscription = data.SubscriptionId is not null
            ? await _repo.FindByProviderSubscriptionAsync(stored.Provider, data.SubscriptionId)
            : null;
        if (subscription is null && data.UserId is Guid owner)
            subscription = await _repo.GetSubscriptionAsync(owner);

        if (subscription is null && data.UserId is null)
        {
            Finish(stored, now, "No user for this event");
            await _repo.SaveChangesAsync();
            return;
        }

        if (subscription is null)
        {
            subscription = new UserSubscription { UserId = data.UserId!.Value, CreatedAt = now };
            await _repo.AddSubscriptionAsync(subscription);
        }

        // Events may come out of order: an older one never undoes a newer state
        if (subscription.LastEventAt is { } last && data.OccurredAt < last)
        {
            Finish(stored, now, "Older than the current state");
            await _repo.SaveChangesAsync();
            return;
        }

        subscription.Provider = stored.Provider;
        subscription.ProviderCustomerId = data.CustomerId ?? subscription.ProviderCustomerId;
        subscription.ProviderSubscriptionId = data.SubscriptionId ?? subscription.ProviderSubscriptionId;
        if (data.Tier is { } tier) subscription.Tier = tier;
        if (data.Status is { } status) subscription.Status = status;
        subscription.CurrentPeriodStart = data.PeriodStart ?? subscription.CurrentPeriodStart;
        subscription.CurrentPeriodEnd = data.PeriodEnd ?? subscription.CurrentPeriodEnd;
        subscription.TrialEndsAt = data.TrialEndsAt;
        subscription.CancelAtPeriodEnd = data.CancelAtPeriodEnd;
        // A paid plan replaces a hand-made one
        subscription.GrantedByAdminId = null;
        subscription.GrantReason = null;
        subscription.LastEventAt = data.OccurredAt;
        subscription.UpdatedAt = now;

        Finish(stored, now, null);
        await _repo.SaveChangesAsync();
    }

    public async Task<string> CreateCheckoutAsync(BillingPlan plan, BillingInterval interval, string? language)
    {
        if (!Enum.IsDefined(plan) || !Enum.IsDefined(interval))
        {
            throw new BadRequestException("Unknown plan or billing period");
        }

        var billing = _providers.FirstOrDefault(x => x.IsConfigured)
                      ?? throw new BillingUnavailableException("Payments are not set up yet");

        var subscription = await _repo.GetSubscriptionAsync(_user.UserId);
        var now = _time.GetUtcNow().UtcDateTime;

        // One paid plan at a time: a running one is changed or cancelled in the provider's portal.
        // A hand-made grant doesn't count, a paid plan replaces it
        if (subscription is { Provider: not SubscriptionProvider.Manual }
            && SubscriptionRules.EffectiveTier(subscription, now) != SubscriptionTier.Free)
        {
            throw new ConflictException("There is a paid plan already, manage it instead");
        }

        var customerId = subscription?.Provider == billing.Provider ? subscription.ProviderCustomerId : null;

        return await billing.CreateCheckoutUrlAsync(
            new BillingCheckout(_user.UserId, _user.Email, plan, interval, customerId, language));
    }

    public async Task<string> CreatePortalAsync(string? language)
    {
        var subscription = await _repo.GetSubscriptionAsync(_user.UserId);
        if (subscription?.ProviderCustomerId is not { } customerId)
        {
            throw new BadRequestException("There is no paid plan to manage");
        }

        var billing = Configured(subscription.Provider)
                      ?? throw new BillingUnavailableException("Payments are not set up yet");

        return await billing.CreatePortalUrlAsync(customerId, language);
    }

    public async Task<BillingStatusDto> GetStatusAsync()
    {
        var subscription = await _repo.GetSubscriptionAsync(_user.UserId);
        var now = _time.GetUtcNow().UtcDateTime;

        return new BillingStatusDto(
            SubscriptionRules.EffectiveTier(subscription, now),
            subscription?.Tier,
            subscription?.Status,
            subscription?.Provider,
            subscription?.CurrentPeriodEnd,
            subscription?.TrialEndsAt,
            subscription?.CancelAtPeriodEnd ?? false,
            _providers.Any(x => x.IsConfigured),
            subscription?.ProviderCustomerId is not null && Configured(subscription.Provider) is not null);
    }

    private IBillingProvider? Configured(SubscriptionProvider provider) =>
        _providers.FirstOrDefault(x => x.IsConfigured && x.Provider == provider);

    private IBillingProvider? Find(string name) =>
        _providers.FirstOrDefault(x => x.IsConfigured && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    private static void Finish(SubscriptionEvent stored, DateTime now, string? error)
    {
        stored.ProcessedAt = now;
        stored.Error = error;
    }
}
