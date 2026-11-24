using System.Globalization;
using System.Text.Json;
using Application.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using static Infrastructure.Billing.StripeJson;

namespace Infrastructure.Billing;

// Promo codes as Stripe coupons and promotion codes. Checkout already shows Stripe's "Add promotion
// code" field, so a code works there without anything else. Batches made here carry a metadata mark,
// so coupons made by hand in the dashboard stay out of the panel
public sealed class StripePromotions : IBillingPromotions
{
    private const string Source = "studytracker-admin";
    // Stripe makes one code per request; a few at a time stay well under its rate limit
    private const int ParallelRequests = 5;

    private readonly StripeApi _api;
    private readonly StripeSettings _settings;
    private readonly ILogger<StripePromotions> _logger;

    public StripePromotions(StripeApi api, StripeSettings settings, ILogger<StripePromotions> logger)
    {
        _api = api;
        _settings = settings;
        _logger = logger;
    }

    public bool IsConfigured => _settings.IsComplete;

    public async Task<string> CreateDiscountAsync(PromoBatch batch, CancellationToken cancellationToken = default)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("name", batch.Name),
            new("max_redemptions", Invariant(batch.TotalUses)),
            new("metadata[source]", Source),
            new("metadata[kind]", batch.Kind.ToString()),
            new("metadata[plan]", batch.Plan?.ToString() ?? "all"),
            new("metadata[new_customers_only]", batch.NewCustomersOnly ? "true" : "false"),
            new("metadata[created_by]", batch.CreatedBy.ToString()),
        };

        if (batch.Kind == PromoKind.FreeMonths)
        {
            // The monthly price off each payment for N months: N free months on a monthly plan,
            // N months' worth off a yearly one, never a free year
            var monthly = await PriceAsync(batch.Plan!.Value, BillingInterval.Month, cancellationToken)
                          ?? throw new BillingUnavailableException("This plan has no monthly price");

            form.Add(new("amount_off", Invariant(monthly.Amount)));
            form.Add(new("currency", monthly.Currency));
            foreach (var (currency, amount) in monthly.Amounts.Where(x => x.Key != monthly.Currency))
                form.Add(new($"currency_options[{currency}][amount_off]", Invariant(amount)));
            form.Add(new("duration", "repeating"));
            form.Add(new("duration_in_months", Invariant(batch.Months!.Value)));
        }
        else
        {
            form.Add(new("percent_off", Invariant(batch.PercentOff)));
            form.Add(new("duration", batch.Duration switch
            {
                PromoDuration.Months => "repeating",
                PromoDuration.Forever => "forever",
                _ => "once",
            }));
            if (batch.Duration == PromoDuration.Months) form.Add(new("duration_in_months", Invariant(batch.Months!.Value)));
        }

        // Only this plan's products, so a code for Plus can't be spent on Duo
        if (batch.Plan is { } plan)
        {
            var products = await ProductsOfAsync(plan, cancellationToken);
            for (var i = 0; i < products.Count; i++) form.Add(new($"applies_to[products][{i}]", products[i]));
        }

        if (batch.ExpiresAt is { } expires) form.Add(new("redeem_by", Invariant(ToUnix(expires))));

        var coupon = await _api.SendAsync(HttpMethod.Post, "v1/coupons", form, "coupon", cancellationToken);
        return Text(coupon, "id") ?? throw new BillingUnavailableException("Stripe gave no coupon id");
    }

    public async Task<IReadOnlyList<string>> AddCodesAsync(
        string discountId, IReadOnlyList<string> codes, PromoCodeLimits limits, CancellationToken cancellationToken = default)
    {
        var created = new bool[codes.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, codes.Count),
            new ParallelOptions { MaxDegreeOfParallelism = ParallelRequests, CancellationToken = cancellationToken },
            async (index, token) =>
            {
                var form = new List<KeyValuePair<string, string>>
                {
                    new("promotion[type]", "coupon"),
                    new("promotion[coupon]", discountId),
                    new("code", codes[index]),
                    new("max_redemptions", Invariant(limits.UsesPerCode)),
                    new("metadata[source]", Source),
                };
                if (limits.NewCustomersOnly) form.Add(new("restrictions[first_time_transaction]", "true"));

                try
                {
                    await _api.SendAsync(HttpMethod.Post, "v1/promotion_codes", form, "promotion code", token);
                    created[index] = true;
                }
                catch (BillingUnavailableException)
                {
                    // Stripe's reason is in the log already; most likely the code is taken
                }
            });

        return codes.Where((_, index) => created[index]).ToList();
    }

    public async Task<IReadOnlyList<PromoBatchInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var coupons = await _api.ListAsync("v1/coupons", "", "coupon", cancellationToken);
        var codes = (await _api.ListAsync("v1/promotion_codes", "", "promotion code", cancellationToken))
            .Select(code => (Coupon: CouponOf(code), Code: code))
            .Where(x => x.Coupon is not null)
            .ToLookup(x => x.Coupon!, x => ToCode(x.Code));

        return coupons
            .Where(coupon => Metadata(coupon, "source") == Source)
            .Select(coupon => ToBatch(coupon, codes[Text(coupon, "id") ?? ""].ToList()))
            // A batch whose codes all failed has nothing to show
            .Where(batch => batch.Codes.Count > 0)
            .OrderByDescending(batch => batch.CreatedAt)
            .ToList();
    }

    public async Task<int> DeactivateAsync(string discountId, CancellationToken cancellationToken = default)
    {
        var path = $"v1/coupons/{Uri.EscapeDataString(discountId)}";
        var coupon = await _api.SendAsync(HttpMethod.Get, path, null, "coupon", cancellationToken, missingIsFine: true);
        if (coupon.ValueKind != JsonValueKind.Object || Metadata(coupon, "source") != Source)
        {
            throw new BadRequestException("No such batch of promo codes");
        }

        var active = await _api.ListAsync("v1/promotion_codes", $"coupon={Uri.EscapeDataString(discountId)}&active=true", "promotion code", cancellationToken);
        var switchedOff = 0;

        await Parallel.ForEachAsync(
            active,
            new ParallelOptions { MaxDegreeOfParallelism = ParallelRequests, CancellationToken = cancellationToken },
            async (code, token) =>
            {
                if (Text(code, "id") is not { } id) return;
                await _api.SendAsync(HttpMethod.Post, $"v1/promotion_codes/{Uri.EscapeDataString(id)}", [new("active", "false")], "promotion code", token);
                Interlocked.Increment(ref switchedOff);
            });

        return switchedOff;
    }

    private sealed record Price(long Amount, string Currency, Dictionary<string, long> Amounts, string? Product);

    private async Task<Price?> PriceAsync(BillingPlan plan, BillingInterval interval, CancellationToken cancellationToken)
    {
        if (!_settings.Prices.TryGetValue((plan, interval), out var priceId)) return null;

        var price = await _api.SendAsync(HttpMethod.Get, $"v1/prices/{Uri.EscapeDataString(priceId)}?expand[]=currency_options",
            null, "price", cancellationToken);
        if (Number(price, "unit_amount") is not { } amount) return null;

        var currency = Text(price, "currency") ?? "eur";
        return new Price(amount, currency, CurrencyAmounts(price, currency, amount), IdOf(price, "product"));
    }

    // Monthly and yearly prices usually share one product, but nothing makes them
    private async Task<List<string>> ProductsOfAsync(BillingPlan plan, CancellationToken cancellationToken)
    {
        var products = new List<string>();
        foreach (var interval in new[] { BillingInterval.Month, BillingInterval.Year })
        {
            if (await PriceAsync(plan, interval, cancellationToken) is { Product: { } product } && !products.Contains(product))
                products.Add(product);
        }

        return products.Count > 0 ? products : throw new BillingUnavailableException("This plan has no prices in Stripe");
    }

    // Since API 2025-09-30 a code points at its coupon through "promotion"; older answers had "coupon"
    private static string? CouponOf(JsonElement code) =>
        code.TryGetProperty("promotion", out var promotion) && promotion.ValueKind == JsonValueKind.Object
            ? IdOf(promotion, "coupon")
            : IdOf(code, "coupon");

    private static PromoCodeInfo ToCode(JsonElement code) => new(
        Text(code, "id") ?? "",
        Text(code, "code") ?? "",
        Flag(code, "active"),
        (int)(Number(code, "times_redeemed") ?? 0),
        Number(code, "max_redemptions") is { } max ? (int)max : null,
        Time(code, "expires_at"));

    private static PromoBatchInfo ToBatch(JsonElement coupon, List<PromoCodeInfo> codes)
    {
        var duration = Text(coupon, "duration") switch
        {
            "repeating" => PromoDuration.Months,
            "forever" => PromoDuration.Forever,
            _ => PromoDuration.Once,
        };

        return new PromoBatchInfo(
            Text(coupon, "id") ?? "",
            Text(coupon, "name") ?? "",
            Enum.TryParse<PromoKind>(Metadata(coupon, "kind"), out var kind) ? kind : PromoKind.PercentOff,
            Enum.TryParse<BillingPlan>(Metadata(coupon, "plan"), out var plan) ? plan : null,
            coupon.TryGetProperty("percent_off", out var percent) && percent.ValueKind == JsonValueKind.Number ? (int)Math.Round(percent.GetDouble()) : null,
            Number(coupon, "amount_off"),
            Text(coupon, "currency"),
            duration,
            duration == PromoDuration.Months ? (int?)Number(coupon, "duration_in_months") : null,
            Metadata(coupon, "new_customers_only") == "true",
            Flag(coupon, "valid"),
            Time(coupon, "created") ?? DateTime.MinValue,
            Time(coupon, "redeem_by"),
            codes.OrderBy(code => code.Code, StringComparer.Ordinal).ToList());
    }

    private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);
}
