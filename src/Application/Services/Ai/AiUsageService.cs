using Application.Ai;
using Application.Constants;
using Application.DTOs.Ai;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Ai;
using Domain.Utility.Subscription;

namespace Application.Services.Ai;

public class AiUsageService : IAiUsageService
{
    // A request is never this slow, a pending one this old was lost with its process
    private static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(15);

    private readonly IAiUsageRepository _repo;
    private readonly ILimitLock _limitLock;
    private readonly AiOptions _options;
    private readonly TimeProvider _time;

    public AiUsageService(IAiUsageRepository repo, ILimitLock limitLock, AiOptions options, TimeProvider time)
    {
        _repo = repo;
        _limitLock = limitLock;
        _options = options;
        _time = time;
    }

    public async Task<Guid> ReserveAsync(Guid userId, AiFeature feature)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        // Same lock pattern as other limits: parallel requests can't both take the last slot
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.AiUsage, userId);

        var tier = await GetTierAsync(userId, now);
        var quota = _options.GetMonthlyQuota(tier, feature);
        var used = await _repo.CountAsync(userId, feature, PeriodStart(now));

        if (used >= quota)
        {
            throw new AiQuotaExceededException(
                $"AI limit reached: {quota} {FeatureName(feature)} per month on the {tier} plan");
        }

        var usage = new AiUsage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Feature = feature,
            Status = AiUsageStatus.Pending,
            CreatedAt = now,
        };

        await _repo.AddAsync(usage);
        await _repo.SaveChangesAsync();
        await limitLock.CommitAsync();

        return usage.Id;
    }

    public async Task CompleteAsync(Guid usageId, AiCompletion completion)
    {
        var usage = await _repo.GetByIdAsync(usageId);
        if (usage is null) return;

        usage.Status = AiUsageStatus.Succeeded;
        usage.Provider = Truncate(completion.Provider, 40);
        usage.Model = Truncate(completion.Model, 100);
        usage.InputTokens = completion.InputTokens;
        usage.OutputTokens = completion.OutputTokens;
        usage.CompletedAt = _time.GetUtcNow().UtcDateTime;

        await _repo.SaveChangesAsync();
    }

    public async Task FailAsync(Guid usageId)
    {
        var usage = await _repo.GetByIdAsync(usageId);
        if (usage is null) return;

        usage.Status = AiUsageStatus.Failed;
        usage.CompletedAt = _time.GetUtcNow().UtcDateTime;

        await _repo.SaveChangesAsync();
    }

    public async Task<AiUsageResponseDto> GetUsageAsync(Guid userId)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var periodStart = PeriodStart(now);
        var tier = await GetTierAsync(userId, now);
        var used = await _repo.CountByFeatureAsync(userId, periodStart);

        return new AiUsageResponseDto
        {
            Tier = tier,
            IsEnabled = _options.Enabled,
            PeriodStart = periodStart,
            ResetsAt = periodStart.AddMonths(1),
            Features = Enum.GetValues<AiFeature>()
                .Select(feature => new AiFeatureUsageDto
                {
                    Feature = feature,
                    Used = used.GetValueOrDefault(feature),
                    Limit = _options.GetMonthlyQuota(tier, feature),
                })
                .ToList(),
        };
    }

    public async Task<int> FailStaleAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        return await _repo.FailPendingAsync(now - PendingTimeout, now);
    }

    private async Task<SubscriptionTier> GetTierAsync(Guid userId, DateTime now)
    {
        var subscription = await _repo.GetSubscriptionAsync(userId);

        if (subscription is null) return SubscriptionTier.Free;
        if (subscription.CurrentPeriodEnd is { } end && end <= now) return SubscriptionTier.Free;

        return subscription.Tier;
    }

    // Quotas reset on the first day of every month, UTC
    private static DateTime PeriodStart(DateTime now) => new(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string FeatureName(AiFeature feature) => feature switch
    {
        AiFeature.QuestionGeneration => "question generations",
        AiFeature.DictionaryGeneration => "word generations",
        AiFeature.AnswerGrading => "answer checks",
        AiFeature.SummaryGeneration => "summary generations",
        AiFeature.MistakeExplanation => "mistake explanations",
        _ => "requests",
    };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
