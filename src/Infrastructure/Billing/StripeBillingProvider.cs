using System.Globalization;
using System.Text;
using System.Text.Json;
using static Infrastructure.Billing.StripeJson;
using Application.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Utility.Subscription;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Billing;

// Stripe Billing over its REST API: Checkout to subscribe, the customer portal to manage, webhooks
// to follow the subscription. Card data never reaches the app, Stripe's pages collect it
public sealed class StripeBillingProvider : IBillingProvider
{
    // Stripe's own default: a captured delivery can't be replayed later
    private static readonly TimeSpan SignatureTolerance = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PricesFor = TimeSpan.FromHours(1);
    private const string PricesCacheKey = "billing:stripe:prices";

    private readonly StripeApi _api;
    private readonly IMemoryCache _cache;
    private readonly StripeSettings _settings;
    private readonly ILogger<StripeBillingProvider> _logger;

    public StripeBillingProvider(StripeApi api, IMemoryCache cache, StripeSettings settings, ILogger<StripeBillingProvider> logger)
    {
        _api = api;
        _cache = cache;
        _settings = settings;
        _logger = logger;
    }

    public SubscriptionProvider Provider => SubscriptionProvider.Stripe;
    public string Name => "stripe";
    public string SignatureHeader => "Stripe-Signature";
    public bool IsConfigured => _settings.IsComplete;

    // Header "t=<unix time>,v1=<hex>[,v1=<hex>]": HMAC-SHA256 of "<t>.<body>" with the endpoint secret.
    // Several v1 values come while a rolled secret and the old one are both valid
    public bool VerifyWebhook(string? signatureHeader, byte[] rawBody, DateTime now)
    {
        if (_settings.WebhookSecret is not { } secret || string.IsNullOrWhiteSpace(signatureHeader)) return false;

        long? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in signatureHeader.Split(','))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;

            var value = pair[1].Trim();
            switch (pair[0].Trim())
            {
                case "t" when long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds):
                    timestamp = seconds;
                    break;
                case "v1":
                    signatures.Add(value);
                    break;
            }
        }

        if (timestamp is not { } signed || signatures.Count == 0) return false;
        if (FromUnix(signed) is not { } signedAt || (now - signedAt).Duration() > SignatureTolerance) return false;

        byte[] payload = [.. Encoding.ASCII.GetBytes($"{signed}."), .. rawBody];
        return signatures.Any(signature => WebhookSignature.IsValidHmacSha256(secret, payload, signature));
    }

    // Only subscription events: they carry the whole state, so the order of other events doesn't matter
    public BillingEvent? ParseEvent(byte[] rawBody)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            var type = root.GetProperty("type").GetString();
            if (type is null || !type.StartsWith("customer.subscription.", StringComparison.Ordinal)) return null;

            var subscription = root.GetProperty("data").GetProperty("object");
            if (Text(subscription, "object") != "subscription") return null;

            // One price per subscription here
            var item = FirstItem(subscription);

            var priceId = item is { } first && first.TryGetProperty("price", out var price) ? Text(price, "id") : null;

            return new BillingEvent(
                Text(root, "id") ?? throw new JsonException("No event id"),
                type,
                Time(root, "created") ?? throw new JsonException("No event time"),
                Guid.TryParse(Metadata(subscription, "user_id"), out var userId) ? userId : null,
                IdOf(subscription, "customer"),
                Text(subscription, "id"),
                _settings.TierOf(priceId),
                StatusOf(Text(subscription, "status")),
                (item is { } a ? Time(a, "current_period_start") : null) ?? Time(subscription, "current_period_start"),
                (item is { } b ? Time(b, "current_period_end") : null) ?? Time(subscription, "current_period_end"),
                Time(subscription, "trial_end"),
                Flag(subscription, "cancel_at_period_end") || Time(subscription, "cancel_at") is not null);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _logger.LogWarning("A signed Stripe event could not be read: {Reason}", exception.Message);
            return null;
        }
    }

    public CreditEvent? ParseCreditEvent(byte[] rawBody)
    {
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            var type = Text(root, "type");
            if (type is null || !BillingEventTypes.IsCredit(type)) return null;

            var item = root.GetProperty("data").GetProperty("object");
            var eventId = Text(root, "id") ?? throw new JsonException("No event id");
            var occurredAt = Time(root, "created") ?? throw new JsonException("No event time");

            if (type == BillingEventTypes.ChargeRefunded)
            {
                return new CreditEvent(eventId, type, occurredAt, CreditEventKind.Refund, null, null,
                    IdOf(item, "payment_intent"), null, Number(item, "amount") ?? 0, Number(item, "amount_refunded") ?? 0);
            }

            // Only the app's credit checkouts, and only once paid: a bank payment completes later
            if (Text(item, "mode") != "payment" || Metadata(item, "kind") != CreditKind) return null;
            if (Text(item, "payment_status") != "paid") return null;

            return new CreditEvent(
                eventId,
                type,
                occurredAt,
                CreditEventKind.Purchase,
                Guid.TryParse(Metadata(item, "user_id"), out var userId) ? userId : null,
                Metadata(item, "pack_id"),
                IdOf(item, "payment_intent"),
                Text(item, "id"),
                Number(item, "amount_total") ?? 0,
                0);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _logger.LogWarning("A signed Stripe payment event could not be read: {Reason}", exception.Message);
            return null;
        }
    }

    public async Task<string> CreateCreditCheckoutUrlAsync(CreditCheckout checkout, CancellationToken cancellationToken = default)
    {
        var userId = checkout.UserId.ToString();
        var currency = checkout.Currency.ToLowerInvariant();
        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            // An ad hoc price from the server's pack, so no Stripe product has to be set up per pack
            new("line_items[0][price_data][currency]", currency),
            new("line_items[0][price_data][unit_amount]", MinorUnits(checkout.Price, currency).ToString(CultureInfo.InvariantCulture)),
            new("line_items[0][price_data][product_data][name]", $"Studytracker AI credits × {checkout.Credits}"),
            new("line_items[0][quantity]", "1"),
            new("success_url", Page("limits?credits=success")),
            new("cancel_url", Page("limits?credits=cancelled")),
            // The webhook grants by these, set here and never by the browser
            new("client_reference_id", userId),
            new("metadata[kind]", CreditKind),
            new("metadata[user_id]", userId),
            new("metadata[pack_id]", checkout.PackId),
            new("payment_intent_data[metadata][kind]", CreditKind),
            new("payment_intent_data[metadata][user_id]", userId),
            new("payment_intent_data[metadata][pack_id]", checkout.PackId),
            new("locale", LocaleOf(checkout.Language)),
        };

        if (checkout.CustomerId is { } customerId) form.Add(new("customer", customerId));
        else if (!string.IsNullOrWhiteSpace(checkout.Email)) form.Add(new("customer_email", checkout.Email));

        // Pack prices include VAT, like the plans
        switch (_settings.TaxMode)
        {
            case StripeTaxMode.Managed:
                form.Add(new("managed_payments[enabled]", "true"));
                form.Add(new("line_items[0][price_data][tax_behavior]", "inclusive"));
                break;
            case StripeTaxMode.Automatic:
                form.Add(new("automatic_tax[enabled]", "true"));
                form.Add(new("billing_address_collection", "required"));
                form.Add(new("line_items[0][price_data][tax_behavior]", "inclusive"));
                break;
        }

        var session = await _api.SendAsync(HttpMethod.Post, "v1/checkout/sessions", form, "credit checkout session", cancellationToken);
        return Text(session, "url") ?? throw new BillingUnavailableException("Stripe gave no checkout address");
    }

    // Marks the app's credit checkouts among other one-off payments
    private const string CreditKind = "ai_credits";

    // Stripe counts in the smallest unit; zero-decimal currencies have none
    private static long MinorUnits(decimal price, string currency) =>
        currency is "jpy" or "krw" or "vnd" or "clp" or "isk" ? (long)Math.Round(price) : (long)Math.Round(price * 100);

    public async Task<string> CreateCheckoutUrlAsync(BillingCheckout checkout, CancellationToken cancellationToken = default)
    {
        if (!_settings.Prices.TryGetValue((checkout.Plan, checkout.Interval), out var priceId))
        {
            throw new BillingUnavailableException("This plan isn't on sale yet");
        }

        var userId = checkout.UserId.ToString();
        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "subscription"),
            new("line_items[0][price]", priceId),
            new("line_items[0][quantity]", "1"),
            new("success_url", Page("limits?checkout=success")),
            new("cancel_url", Page("limits?checkout=cancelled")),
            // The webhook finds the user by these, set here and never by the browser
            new("client_reference_id", userId),
            new("metadata[user_id]", userId),
            new("subscription_data[metadata][user_id]", userId),
            new("allow_promotion_codes", "true"),
            new("locale", LocaleOf(checkout.Language)),
        };

        if (checkout.CustomerId is { } customerId) form.Add(new("customer", customerId));
        else if (!string.IsNullOrWhiteSpace(checkout.Email)) form.Add(new("customer_email", checkout.Email));

        // A trial only for someone who never paid before
        if (_settings.TrialDays > 0 && checkout.CustomerId is null)
        {
            form.Add(new("subscription_data[trial_period_days]", _settings.TrialDays.ToString(CultureInfo.InvariantCulture)));
        }

        switch (_settings.TaxMode)
        {
            // Stripe sells as the merchant of record; tax parameters are its own then
            case StripeTaxMode.Managed:
                form.Add(new("managed_payments[enabled]", "true"));
                break;
            // VAT depends on where the buyer lives, so the address is asked for
            case StripeTaxMode.Automatic:
                form.Add(new("automatic_tax[enabled]", "true"));
                form.Add(new("billing_address_collection", "required"));
                form.Add(new("tax_id_collection[enabled]", "true"));
                if (checkout.CustomerId is not null)
                {
                    form.Add(new("customer_update[address]", "auto"));
                    form.Add(new("customer_update[name]", "auto"));
                }
                break;
        }

        var session = await _api.SendAsync(HttpMethod.Post, "v1/checkout/sessions", form, "checkout session", cancellationToken);
        return Text(session, "url") ?? throw new BillingUnavailableException("Stripe gave no checkout address");
    }

    public async Task<string> CreatePortalUrlAsync(
        string customerId, string? language, string? changePlanOf = null, CancellationToken cancellationToken = default)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("customer", customerId),
            new("return_url", Page("limits")),
            new("locale", LocaleOf(language)),
        };

        if (changePlanOf is not null)
        {
            try
            {
                return await PortalSessionAsync(
                [
                    .. form,
                    new("flow_data[type]", "subscription_update"),
                    new("flow_data[subscription_update][subscription]", changePlanOf),
                    new("flow_data[after_completion][type]", "redirect"),
                    new("flow_data[after_completion][redirect][return_url]", Page("limits")),
                ], cancellationToken);
            }
            catch (BillingUnavailableException)
            {
                // Switching plans is off in the portal's settings (Stripe dashboard → Settings → Billing →
                // Customer portal): the usual portal still lets the user manage the plan
                _logger.LogWarning("Stripe portal: switching plans isn't enabled, opening the usual portal");
            }
        }

        return await PortalSessionAsync(form, cancellationToken);
    }

    private async Task<string> PortalSessionAsync(List<KeyValuePair<string, string>> form, CancellationToken cancellationToken)
    {
        var session = await _api.SendAsync(HttpMethod.Post, "v1/billing_portal/sessions", form, "portal session", cancellationToken);
        return Text(session, "url") ?? throw new BillingUnavailableException("Stripe gave no portal address");
    }

    public async Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await _api.SendAsync(HttpMethod.Delete, $"v1/customers/{Uri.EscapeDataString(customerId)}", null, "customer deletion", cancellationToken, missingIsFine: true);
    }

    public async Task<IReadOnlyList<BillingPrice>> GetPricesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(PricesCacheKey, out IReadOnlyList<BillingPrice>? cached) && cached is not null) return cached;

        var loaded = await Task.WhenAll(_settings.Prices.Select(async pair =>
        {
            // The amounts in other currencies come only when asked for
            var price = await _api.SendAsync(HttpMethod.Get, $"v1/prices/{Uri.EscapeDataString(pair.Value)}?expand[]=currency_options",
                null, "price", cancellationToken);
            var interval = price.TryGetProperty("recurring", out var recurring) ? Text(recurring, "interval") : null;
            var expected = pair.Key.Interval == BillingInterval.Year ? "year" : "month";

            // A price for another period or an archived one would sell the wrong thing
            if (!Flag(price, "active") || interval != expected
                || !price.TryGetProperty("unit_amount", out var amount) || amount.ValueKind != JsonValueKind.Number)
            {
                _logger.LogWarning("Stripe price for {Plan} {Interval} is skipped: inactive, not {Expected}ly or without an amount",
                    pair.Key.Plan, pair.Key.Interval, expected);
                return null;
            }

            // Managed Payments adds VAT on top of an exclusive price, so the buyer would pay more than the card shows
            if (_settings.TaxMode == StripeTaxMode.Managed && Text(price, "tax_behavior") != "inclusive")
            {
                _logger.LogWarning("Stripe price for {Plan} {Interval} doesn't include tax: buyers pay VAT on top of the shown amount",
                    pair.Key.Plan, pair.Key.Interval);
            }

            var currency = Text(price, "currency") ?? "eur";
            return new BillingPrice(pair.Key.Plan, pair.Key.Interval, amount.GetInt64(), currency,
                CurrencyAmounts(price, currency, amount.GetInt64()));
        }));

        var prices = loaded.OfType<BillingPrice>().ToList();
        _cache.Set(PricesCacheKey, (IReadOnlyList<BillingPrice>)prices, PricesFor);
        return prices;
    }

    private string Page(string relative) => new Uri(_settings.SiteUrl!, relative).ToString();


    // The price's main amount and every currency option with a plain amount, keys in lower case
    // Unpaid, incomplete (the first payment isn't through), expired, paused and ended plans give no access
    private static SubscriptionStatus StatusOf(string? status) => status switch
    {
        "active" => SubscriptionStatus.Active,
        "trialing" => SubscriptionStatus.Trialing,
        "past_due" => SubscriptionStatus.PastDue,
        _ => SubscriptionStatus.Expired,
    };

    // Checkout speaks these interface languages; Ukrainian follows the browser
    private static string LocaleOf(string? language) => language switch
    {
        "nb" => "nb",
        "en-GB" => "en-GB",
        _ => "auto",
    };
}
