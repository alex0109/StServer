using Domain.Utility.Ai;
using Domain.Utility.Subscription;

namespace Application.DTOs.Admin;

// Configured unit costs against what requests really cost, so the config can be corrected from data.
// Costs in USD: tokens at AiPricing's prices, podcast voices by spoken characters
public record AdminAiFeatureCostDto(AiFeature Feature, int Requests, long Units, decimal ActualCost, decimal ActualPerUnit, decimal ConfiguredPerUnit);

// One user's AI cost this calendar month against what their plan leaves after VAT and fees per seat
public record AdminAiUserCostDto(Guid UserId, string? Email, SubscriptionTier Tier, decimal Cost, decimal NetRevenue)
{
    public bool OverRevenue => Cost > NetRevenue;
}

public record AdminAiMarginsDto(
    DateTime MonthStart,
    // Share of fallback answers among requests of features that have one
    decimal FallbackShare7Days,
    decimal FallbackShareConfigured,
    List<AdminAiFeatureCostDto> Features,
    List<AdminAiUserCostDto> TopUsers,
    int CreditsSoldThisMonth,
    int CreditsSpentThisMonth,
    DateTime GeneratedAt);

// Raw numbers for the margins, grouped by the database
public record AiCostRow(AiFeature Feature, bool IsFallback, bool HasCost, int Requests, long Units, decimal Cost);

public record AiUserCostRow(Guid UserId, decimal Cost);
