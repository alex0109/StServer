using Application.Ai.Pricing;
using Domain.Utility.Ai;
using Domain.Utility.Subscription;

namespace Application.DTOs.Ai;

// AI use in the current period, per feature, and the credit wallet
public class AiUsageResponseDto
{
    public required SubscriptionTier Tier { get; init; }
    public required bool IsEnabled { get; init; }
    public required DateTime PeriodStart { get; init; }
    public required DateTime ResetsAt { get; init; }
    public required List<AiFeatureUsageDto> Features { get; init; }
    public int CreditBalance { get; init; }
    // Study AI takes credits without asking once its quota is used up
    public bool AutoUseCredits { get; init; }
}

// Units: requests, words, podcasts or video minutes
public class AiFeatureUsageDto
{
    public required AiFeature Feature { get; init; }
    public AiBucket Bucket { get; init; }
    public required int Used { get; init; }
    public required int Limit { get; init; }
    // Once per account (Free's media)
    public int TrialUsed { get; init; }
    public int TrialLimit { get; init; }
    // Credits per block of units; null: not sold for credits
    public int? Credits { get; init; }
    public int CreditBlock { get; init; } = 1;
}
