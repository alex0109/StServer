using Application.Billing;

namespace Application.DTOs.Billing;

// A batch of promo codes as the admin panel asks for it
public class PromoBatchCreateDto
{
    // Shown in Stripe and in the panel, e.g. "Deichman Bjørvika, autumn"
    public string? Name { get; set; }
    public PromoKind Kind { get; set; } = PromoKind.FreeMonths;
    // Required for free months; null for a discount on both Plus and Duo
    public BillingPlan? Plan { get; set; }
    public int PercentOff { get; set; }
    public PromoDuration Duration { get; set; } = PromoDuration.Months;
    public int? Months { get; set; }
    public int Count { get; set; } = 1;
    public int UsesPerCode { get; set; } = 1;
    // The exact code for a single one, a prefix for many; empty for random codes
    public string? Code { get; set; }
    public DateTime? ExpiresAt { get; set; }
    // Only for someone who never paid before
    public bool NewCustomersOnly { get; set; }
}

public record AdminPromoCodesDto(bool Configured, IReadOnlyList<PromoBatchInfo> Batches);

// Requested may be more than the codes made, when Stripe refused some
public record PromoBatchCreatedDto(string Id, IReadOnlyList<string> Codes, int Requested);
