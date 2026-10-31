using Application.Billing;
using Application.Limits;
using Domain.Utility.Ai;
using Domain.Utility.Subscription;

namespace Application.DTOs.Billing;

// What the user sees about their plan
public record BillingStatusDto(
    SubscriptionTier Tier,
    SubscriptionTier? PlanTier,
    SubscriptionStatus? Status,
    SubscriptionProvider? Provider,
    DateTime? CurrentPeriodEnd,
    DateTime? TrialEndsAt,
    bool CancelAtPeriodEnd,
    // A provider is configured, so "upgrade" can be offered
    bool CheckoutAvailable,
    // A paid plan the provider's portal can manage: card, invoices, cancelling
    bool CanManage);

public class CheckoutCreateDto
{
    public BillingPlan Plan { get; set; } = BillingPlan.Plus;
    public BillingInterval Interval { get; set; } = BillingInterval.Month;
    // Interface language, so the provider's page opens in it
    public string? Language { get; set; }
}

public class PortalCreateDto
{
    public string? Language { get; set; }
}

public record BillingPriceDto(BillingInterval Interval, long Amount, string Currency);

// One tier: its prices (none for Free) and what it allows. Null limits mean no limit
public record BillingPlanDto(
    SubscriptionTier Tier,
    List<BillingPriceDto> Prices,
    TierLimits Limits,
    Dictionary<AiFeature, int> AiPerMonth);

public record BillingPlansDto(bool CheckoutAvailable, List<BillingPlanDto> Plans);

public record CheckoutDto(string Url);

public class SubscriptionGrantDto
{
    public Guid UserId { get; set; }
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Plus;
    // Null: until revoked
    public DateTime? Until { get; set; }
    public string? Reason { get; set; }
}

public record AdminSubscriptionDto(
    Guid UserId,
    SubscriptionTier Tier,
    SubscriptionTier EffectiveTier,
    SubscriptionStatus Status,
    SubscriptionProvider Provider,
    // Opens the customer in the provider's dashboard
    string? ProviderCustomerId,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    string? GrantReason,
    Guid? GrantedByAdminId,
    DateTime UpdatedAt);
