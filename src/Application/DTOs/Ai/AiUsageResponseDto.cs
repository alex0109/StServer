using Domain.Utility.Ai;
using Domain.Utility.Subscription;

namespace Application.DTOs.Ai;

public class AiUsageResponseDto
{
    public required SubscriptionTier Tier { get; init; }
    public required bool IsEnabled { get; init; }
    public required DateTime PeriodStart { get; init; }
    public required DateTime ResetsAt { get; init; }
    public required List<AiFeatureUsageDto> Features { get; init; }
}

public class AiFeatureUsageDto
{
    public required AiFeature Feature { get; init; }
    public required int Used { get; init; }
    public required int Limit { get; init; }
}
