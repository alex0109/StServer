namespace Application.Billing;

// Free months: the plan's monthly price off each payment for that many months, so a yearly plan
// gets those months off and not a free year. Percent off: a share of the price, once, for some months
// or for good
public enum PromoKind
{
    FreeMonths = 1,
    PercentOff = 2,
}

public enum PromoDuration
{
    Once = 1,
    Months = 2,
    Forever = 3,
}

// One discount and the codes that give it. Plan null: both Plus and Duo (percent off only)
public record PromoBatch(
    string Name,
    PromoKind Kind,
    BillingPlan? Plan,
    int PercentOff,
    PromoDuration Duration,
    int? Months,
    int TotalUses,
    bool NewCustomersOnly,
    DateTime? ExpiresAt,
    Guid CreatedBy);

// A code's own limits, the same for every code of a batch
public record PromoCodeLimits(int UsesPerCode, bool NewCustomersOnly);

// Read back from the provider
public record PromoCodeInfo(string Id, string Code, bool Active, int TimesRedeemed, int? MaxRedemptions, DateTime? ExpiresAt);

public record PromoBatchInfo(
    string Id,
    string Name,
    PromoKind Kind,
    BillingPlan? Plan,
    int? PercentOff,
    long? AmountOff,
    string? Currency,
    PromoDuration Duration,
    int? Months,
    bool NewCustomersOnly,
    // The discount can still be applied to new payments
    bool Valid,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    List<PromoCodeInfo> Codes);
