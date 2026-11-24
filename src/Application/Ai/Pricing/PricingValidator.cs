using System.Globalization;
using System.Text;
using Domain.Utility.Ai;
using Domain.Utility.Subscription;

namespace Application.Ai.Pricing;

// Margin guardrail: a pricing config that loses money is a list of violations. A unit test runs it
// on the shipped config and the API logs them at startup
public static class PricingValidator
{
    public static List<string> Validate(PricingOptions options)
    {
        var violations = new List<string>();
        var lowestNet = options.LowestNetPerCredit();

        foreach (var feature in Enum.GetValues<AiFeature>())
        {
            if (options.FeatureOf(feature) is null) violations.Add($"{feature}: no unit cost");
        }

        foreach (var plan in PaidPlans(options))
        {
            if (plan.NetRevenue < plan.WorstCost)
                violations.Add($"{plan.Tier} at {plan.Price}: worst-case AI cost {Usd(plan.WorstCost)} is above net revenue {Usd(plan.NetRevenue)}");
        }

        foreach (var feature in CreditFeatures(options))
        {
            if (feature.Revenue < 2 * feature.Cost)
                violations.Add($"{feature.Feature}: {feature.Credits} credits bring {Usd(feature.Revenue)}, below 2 × {Usd(feature.Cost)}");
        }

        if (options.Packs.Count == 0) violations.Add("No credit packs");
        if (lowestNet <= 0) violations.Add("A credit pack earns nothing after VAT and fees");

        var free = WorstCost(options, SubscriptionTier.Free, trial: false);
        if (free > options.FreeMonthlyCostCap)
            violations.Add($"Free: worst-case monthly cost {Usd(free)} is above {Usd(options.FreeMonthlyCostCap)}");

        var trial = WorstCost(options, SubscriptionTier.Free, trial: true);
        if (trial > options.FreeTrialCostCap)
            violations.Add($"Free trial: one-time cost {Usd(trial)} is above {Usd(options.FreeTrialCostCap)}");

        return violations;
    }

    // Worst case of one subscription: every quota used up on every seat
    public static decimal WorstCost(PricingOptions options, SubscriptionTier tier, bool trial = false)
    {
        if (options.PlanOf(tier) is not { } plan) return 0;
        var units = trial ? plan.LifetimeTrial : plan.Quotas;
        return units.Sum(x => x.Value * options.BlendedCost(x.Key)) * (trial ? 1 : plan.Seats);
    }

    public record PlanMargin(SubscriptionTier Tier, string Price, decimal NetRevenue, decimal WorstCost)
    {
        public decimal Margin => NetRevenue - WorstCost;
    }

    public record CreditMargin(AiFeature Feature, int Credits, int Block, decimal Cost, decimal Revenue)
    {
        public decimal Multiple => Cost == 0 ? 0 : Revenue / Cost;
    }

    // Each price of each sold plan; a yearly price is compared with twelve months of use
    public static IEnumerable<PlanMargin> PaidPlans(PricingOptions options) =>
        options.Plans
            .Where(plan => plan.Value.Prices.Count > 0)
            .SelectMany(plan => plan.Value.Prices.Select(price =>
            {
                var months = price.Interval == Billing.BillingInterval.Year ? 12 : 1;
                return new PlanMargin(
                    plan.Key,
                    $"{price.Amount.ToString(CultureInfo.InvariantCulture)} {price.Currency}/{price.Interval}",
                    options.NetRevenue(price.Amount, price.Currency),
                    WorstCost(options, plan.Key) * months);
            }));

    // Credits of one charged block against twice its cost
    public static IEnumerable<CreditMargin> CreditFeatures(PricingOptions options)
    {
        var lowestNet = options.LowestNetPerCredit();
        return options.Features
            .Where(x => x.Value.Credits is not null)
            .Select(x =>
            {
                var block = Math.Max(1, x.Value.CreditBlock);
                return new CreditMargin(x.Key, x.Value.Credits!.Value, block, options.BlendedCost(x.Key) * block, x.Value.Credits.Value * lowestNet);
            });
    }

    public static string MarginTable(PricingOptions options)
    {
        var text = new StringBuilder();
        text.AppendLine("Plan      Price                 Net revenue  Worst-case cost  Margin");
        foreach (var plan in PaidPlans(options))
            text.AppendLine($"{plan.Tier,-9} {plan.Price,-21} {Usd(plan.NetRevenue),11}  {Usd(plan.WorstCost),15}  {Usd(plan.Margin),7}");

        text.AppendLine($"Free      monthly worst case {Usd(WorstCost(options, SubscriptionTier.Free))} (cap {Usd(options.FreeMonthlyCostCap)}), trial once {Usd(WorstCost(options, SubscriptionTier.Free, trial: true))} (cap {Usd(options.FreeTrialCostCap)})");
        text.AppendLine();
        text.AppendLine($"Lowest net per credit: {options.LowestNetPerCredit().ToString("0.00000", CultureInfo.InvariantCulture)}");
        text.AppendLine("Feature               Credits  Per units  Block cost  Credit value  Multiple");
        foreach (var feature in CreditFeatures(options))
            text.AppendLine($"{feature.Feature,-21} {feature.Credits,7}  {feature.Block,9}  {Usd(feature.Cost),10}  {Usd(feature.Revenue),12}  {feature.Multiple.ToString("0.00", CultureInfo.InvariantCulture),7}×");

        return text.ToString();
    }

    private static string Usd(decimal value) => "$" + value.ToString("0.0000", CultureInfo.InvariantCulture);
}
