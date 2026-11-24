using Application.Billing;
using Domain.Utility.Ai;
using Domain.Utility.Subscription;

namespace Application.Ai.Pricing;

// The cost model: what each AI feature costs, what each plan gives and costs, and what credits
// cost. Every number lives in configuration ("Pricing"); PricingValidator checks they stay profitable
public class PricingOptions
{
    public const string SectionName = "Pricing";

    // Share of requests answered by the fallback provider, which costs more on text features
    public decimal FallbackShare { get; set; }
    public decimal StripePercent { get; set; }
    public decimal StripeFixedNok { get; set; }
    // VAT is assumed even before registration, so the margins hold after it
    public decimal VatRate { get; set; }
    public decimal NokPerUsd { get; set; } = 1;

    public decimal FreeMonthlyCostCap { get; set; }
    public decimal FreeTrialCostCap { get; set; }

    public Dictionary<AiFeature, FeaturePricing> Features { get; set; } = new();
    public Dictionary<SubscriptionTier, PlanPricing> Plans { get; set; } = new();
    public List<CreditPack> Packs { get; set; } = [];

    public FeaturePricing? FeatureOf(AiFeature feature) => Features.GetValueOrDefault(feature);

    // Pro is no longer sold; the plans granted by hand before keep Plus's quotas
    public PlanPricing? PlanOf(SubscriptionTier tier) =>
        Plans.GetValueOrDefault(tier) ?? (tier == SubscriptionTier.Pro ? Plans.GetValueOrDefault(SubscriptionTier.Plus) : null);

    public int QuotaOf(SubscriptionTier tier, AiFeature feature) => PlanOf(tier)?.Quotas.GetValueOrDefault(feature) ?? 0;

    public int TrialOf(SubscriptionTier tier, AiFeature feature) => PlanOf(tier)?.LifetimeTrial.GetValueOrDefault(feature) ?? 0;

    public CreditPack? PackOf(string? id) => Packs.FirstOrDefault(pack => pack.Id == id);

    // One unit with the expected share of fallback answers
    public decimal BlendedCost(AiFeature feature) =>
        FeatureOf(feature) is { } pricing
            ? pricing.UnitCost * (1 - FallbackShare) + (pricing.FallbackUnitCost ?? pricing.UnitCost) * FallbackShare
            : 0;

    public decimal ToUsd(decimal amount, string currency) =>
        string.Equals(currency, "NOK", StringComparison.OrdinalIgnoreCase) ? amount / NokPerUsd : amount;

    // What is left of a payment after VAT and Stripe's fees, in USD
    public decimal NetRevenue(decimal amount, string currency)
    {
        var usd = ToUsd(amount, currency);
        return usd / (1 + VatRate) - (usd * StripePercent + StripeFixedNok / NokPerUsd);
    }

    public decimal NetPerCredit(CreditPack pack) => pack.Credits > 0 ? NetRevenue(pack.Price, pack.Currency) / pack.Credits : 0;

    // What a month of a plan leaves per seat after VAT and fees; Free has its cost cap instead
    public decimal MonthlyNetPerSeat(SubscriptionTier tier)
    {
        if (tier == SubscriptionTier.Free) return FreeMonthlyCostCap;
        if (PlanOf(tier) is not { } plan || plan.Prices.Count == 0) return 0;

        var monthly = plan.Prices
            .Select(price => NetRevenue(price.Amount, price.Currency) / (price.Interval == BillingInterval.Year ? 12 : 1))
            .Min();
        return monthly / Math.Max(1, plan.Seats);
    }

    // The cheapest a credit is sold for: margins hold whichever pack it came from
    public decimal LowestNetPerCredit() => Packs.Count == 0 ? 0 : Packs.Min(NetPerCredit);
}

public class FeaturePricing
{
    // USD per unit with the main provider, and with the fallback; no fallback: the main cost
    public decimal UnitCost { get; set; }
    public decimal? FallbackUnitCost { get; set; }

    // Credits per block of units; null: not sold for credits
    public int? Credits { get; set; }
    public int CreditBlock { get; set; } = 1;
    // A started block covers the next requests too, for features whose request is smaller than a block
    public bool SharedCreditBlocks { get; set; }

    // Longest input a request may send, the size the unit cost assumes
    public int? MaxInputChars { get; set; }
}

public class PlanPricing
{
    // Each seat has the full quota: a Duo is two
    public int Seats { get; set; } = 1;
    // What customers pay, next to the Stripe price ids (STRIPE_PRICE_*)
    public List<PlanPrice> Prices { get; set; } = [];
    // Units per seat per period
    public Dictionary<AiFeature, int> Quotas { get; set; } = new();
    // Units once per account, for trying what the plan has no quota of
    public Dictionary<AiFeature, int> LifetimeTrial { get; set; } = new();
}

public class PlanPrice
{
    public BillingInterval Interval { get; set; } = BillingInterval.Month;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
}

public class CreditPack
{
    public string Id { get; set; } = "";
    public int Credits { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
}
