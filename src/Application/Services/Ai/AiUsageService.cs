using Application.Admin;
using Application.Ai;
using Application.Ai.Pricing;
using Application.Constants;
using Application.DTOs.Ai;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Ai;

namespace Application.Services.Ai;

// The usage gate. Units come from the plan's quota in its period, then from Free's one-time media
// trial, then from credits the user agreed to spend; otherwise a typed denial says what would help
public class AiUsageService : IAiUsageService
{
    // No request runs this long; one pending for longer was lost with its process
    public static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(10);

    private readonly IAiUsageRepository _repo;
    private readonly ICreditRepository _credits;
    private readonly CreditLedger _ledger;
    private readonly ILimitLock _limitLock;
    private readonly ISubscriptionTierProvider _plans;
    private readonly PricingOptions _pricing;
    private readonly AiPricingOptions _prices;
    private readonly AiOptions _options;
    private readonly IAiCreditConsent? _consent;
    private readonly TimeProvider _time;

    public AiUsageService(
        IAiUsageRepository repo,
        ICreditRepository credits,
        ILimitLock limitLock,
        ISubscriptionTierProvider plans,
        PricingOptions pricing,
        AiPricingOptions prices,
        AiOptions options,
        TimeProvider time,
        IAiCreditConsent? consent = null)
    {
        _repo = repo;
        _credits = credits;
        _ledger = new CreditLedger(credits, time);
        _limitLock = limitLock;
        _plans = plans;
        _pricing = pricing;
        _prices = prices;
        _options = options;
        _time = time;
        _consent = consent;
    }

    public async Task<AiReservation> ReserveAsync(Guid userId, AiFeature feature, int units = 1, bool allowCredits = true)
    {
        units = Math.Max(1, units);
        var now = _time.GetUtcNow().UtcDateTime;

        // One lock per user for quota and credits: parallel requests can't both take the last of either
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.AiUsage, userId);

        var plan = await _plans.GetAiPlanAsync(userId);
        var quota = _pricing.QuotaOf(plan.Tier, feature);
        var remaining = quota - await _repo.SumUnitsAsync(userId, feature, AiUsageSource.Quota, plan.Start);

        var usage = new AiUsage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Feature = feature,
            Status = AiUsageStatus.Pending,
            Units = units,
            CreatedAt = now,
        };

        var trialSpent = false;
        if (remaining >= units)
        {
            usage.Source = AiUsageSource.Quota;
        }
        else if (await TrialLeftAsync(userId, plan.Tier, feature) is { } trialLeft && trialLeft >= units)
        {
            usage.Source = AiUsageSource.Trial;
        }
        else
        {
            trialSpent = _pricing.TrialOf(plan.Tier, feature) > 0;
            await PayWithCreditsAsync(usage, Math.Max(0, remaining), trialSpent, allowCredits);
        }

        await _repo.AddAsync(usage);
        await _repo.SaveChangesAsync();
        await limitLock.CommitAsync();

        return new AiReservation(usage.Id, usage.Source, usage.Units, usage.Credits);
    }

    public async Task CommitAsync(Guid usageId, AiCompletion completion, int? units = null, int? videoSeconds = null)
    {
        var usage = await _repo.GetByIdAsync(usageId);
        if (usage is null || usage.Status != AiUsageStatus.Pending) return;

        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.AiUsage, usage.UserId);

        await CommitLockedAsync(usage, completion, units, videoSeconds);

        await _repo.SaveChangesAsync();
        await limitLock.CommitAsync();
    }

    public async Task ReleaseAsync(Guid usageId, AiCompletion? completion = null)
    {
        var usage = await _repo.GetByIdAsync(usageId);
        if (usage is null || usage.Status != AiUsageStatus.Pending) return;

        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.AiUsage, usage.UserId);

        await ReleaseLockedAsync(usage, completion);

        await _repo.SaveChangesAsync();
        await limitLock.CommitAsync();
    }

    public async Task SettleUnusableAsync(Guid usageId, AiCompletion completion)
    {
        var usage = await _repo.GetByIdAsync(usageId);
        if (usage is null || usage.Status != AiUsageStatus.Pending) return;

        // Counted under the same lock as the release, so parallel empty answers can't all be forgiven
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.AiUsage, usage.UserId);

        var since = _time.GetUtcNow().UtcDateTime - TimeSpan.FromDays(1);
        var forgiven = await _repo.CountForgivenAsync(usage.UserId, usage.Feature, since);

        if (forgiven < Math.Max(0, _options.UnusableGracePerDay)) await ReleaseLockedAsync(usage, completion);
        // The whole reservation: an empty answer says nothing about how much of a video was watched
        else await CommitLockedAsync(usage, completion, units: null, videoSeconds: null);

        await _repo.SaveChangesAsync();
        await limitLock.CommitAsync();
    }

    private async Task CommitLockedAsync(AiUsage usage, AiCompletion completion, int? units, int? videoSeconds)
    {
        // A shorter video than reserved: the rest of the minutes, or of the credits, goes back
        if (units is { } used && used >= 1 && used < usage.Units)
        {
            if (usage.Source == AiUsageSource.Credits && _pricing.FeatureOf(usage.Feature) is { } pricing)
            {
                var credits = CreditMath.CreditsFor(pricing, used);
                if (credits < usage.Credits)
                {
                    await ChangeBalanceAsync(usage, usage.Credits - credits, CreditReason.Return, $"return:{usage.Id}:partial");
                    usage.Credits = credits;
                }
            }

            usage.Units = used;
        }

        Record(usage, completion, AiUsageStatus.Succeeded);
        usage.VideoSeconds = videoSeconds ?? usage.VideoSeconds;
    }

    private async Task ReleaseLockedAsync(AiUsage usage, AiCompletion? completion)
    {
        // A failed request counts nowhere: the quota place frees itself, credits come back
        if (usage.Source == AiUsageSource.Credits && usage.Credits > 0)
            await ChangeBalanceAsync(usage, usage.Credits, CreditReason.Return, $"return:{usage.Id}");

        if (completion is not null) Record(usage, completion, AiUsageStatus.Failed);
        else usage.Status = AiUsageStatus.Failed;
        usage.CompletedAt = _time.GetUtcNow().UtcDateTime;
    }

    public async Task<int> ReleaseStaleAsync()
    {
        var stale = await _repo.ListPendingAsync(_time.GetUtcNow().UtcDateTime - PendingTimeout);
        foreach (var id in stale) await ReleaseAsync(id);
        return stale.Count;
    }

    public async Task<AiUsageResponseDto> GetUsageAsync(Guid userId)
    {
        var plan = await _plans.GetAiPlanAsync(userId);
        var used = await _repo.SumUnitsByFeatureAsync(userId, AiUsageSource.Quota, plan.Start);
        var trialUsed = await _repo.SumUnitsByFeatureAsync(userId, AiUsageSource.Trial, DateTime.MinValue);
        var wallet = await _credits.GetWalletAsync(userId);

        return new AiUsageResponseDto
        {
            Tier = plan.Tier,
            IsEnabled = _options.Enabled,
            PeriodStart = plan.Start,
            ResetsAt = plan.End,
            CreditBalance = wallet?.Balance ?? 0,
            AutoUseCredits = wallet?.AutoUseForStudy ?? false,
            Features = Enum.GetValues<AiFeature>()
                .Select(feature => new AiFeatureUsageDto
                {
                    Feature = feature,
                    Bucket = AiBuckets.Of(feature),
                    Used = used.GetValueOrDefault(feature),
                    Limit = _pricing.QuotaOf(plan.Tier, feature),
                    TrialUsed = trialUsed.GetValueOrDefault(feature),
                    TrialLimit = _pricing.TrialOf(plan.Tier, feature),
                    Credits = _pricing.FeatureOf(feature)?.Credits,
                    CreditBlock = _pricing.FeatureOf(feature)?.CreditBlock ?? 1,
                })
                .ToList(),
        };
    }

    private async Task<int?> TrialLeftAsync(Guid userId, Domain.Utility.Subscription.SubscriptionTier tier, AiFeature feature)
    {
        var trial = _pricing.TrialOf(tier, feature);
        if (trial <= 0) return null;
        return trial - await _repo.SumUnitsAsync(userId, feature, AiUsageSource.Trial, DateTime.MinValue);
    }

    // Credits pay for the whole request or not at all; media always ask first, study AI asks until
    // auto-use is switched on
    private async Task PayWithCreditsAsync(AiUsage usage, int remaining, bool trialSpent, bool allowCredits)
    {
        var feature = usage.Feature;
        var pricing = _pricing.FeatureOf(feature);
        var wallet = await _credits.GetWalletAsync(usage.UserId);
        var balance = wallet?.Balance ?? 0;

        // Not sold for credits (the AI answer check degrades to the plain one instead)
        if (pricing?.Credits is null)
            throw Denied(trialSpent ? AiDenialCode.TrialUsed : AiDenialCode.QuotaExhausted, null);

        var paidBefore = pricing.SharedCreditBlocks
            ? await _repo.SumUnitsAsync(usage.UserId, feature, AiUsageSource.Credits, DateTime.MinValue)
            : 0;
        var credits = CreditMath.CreditsFor(pricing, usage.Units, paidBefore);

        var allowed = allowCredits
                      && (_consent?.UseCredits == true || (AiBuckets.Of(feature) == AiBucket.Study && wallet?.AutoUseForStudy == true));
        if (!allowed)
            throw Denied(trialSpent ? AiDenialCode.TrialUsed : AiDenialCode.QuotaExhausted, credits);
        if (balance < credits)
            throw Denied(AiDenialCode.InsufficientCredits, credits);

        usage.Source = AiUsageSource.Credits;
        usage.Credits = credits;
        // A block already paid by an earlier request: nothing to take
        if (credits > 0) await ChangeBalanceAsync(usage, -credits, CreditReason.Spend, $"spend:{usage.Id}");

        AiUsageDeniedException Denied(AiDenialCode code, int? required) => new(
            code,
            feature,
            code switch
            {
                AiDenialCode.InsufficientCredits => $"Not enough credits: {required} needed, {balance} left",
                AiDenialCode.TrialUsed => "The free trial of this feature is used up",
                _ => "The AI quota of this feature is used up for this period",
            },
            remaining,
            required,
            balance);
    }

    private async Task ChangeBalanceAsync(AiUsage usage, int delta, CreditReason reason, string key)
    {
        var entry = _ledger.Entry(usage.UserId, delta, reason, key);
        entry.UsageId = usage.Id;
        entry.Feature = usage.Feature;
        await _ledger.ChangeAsync(entry);
    }

    // Tokens, the provider that answered and what it cost at the configured prices
    private void Record(AiUsage usage, AiCompletion completion, AiUsageStatus status)
    {
        usage.Status = status;
        usage.Provider = Truncate(completion.Provider, 40);
        usage.Model = Truncate(completion.Model, 100);
        usage.InputTokens = completion.InputTokens;
        usage.OutputTokens = completion.OutputTokens;
        usage.EstimatedCost = _prices.Cost(completion.Model, completion.InputTokens, completion.OutputTokens);
        usage.CompletedAt = _time.GetUtcNow().UtcDateTime;

        // Only features with a fallback can be answered by one; the first provider in order is the main one
        var primary = (_options.ProviderOrder is { Length: > 0 } order ? order : AiOptions.DefaultProviderOrder)[0];
        usage.IsFallback = _pricing.FeatureOf(usage.Feature)?.FallbackUnitCost is not null
                           && !string.Equals(completion.Provider, primary, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
