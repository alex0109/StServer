using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
    public const string HttpClientName = "Stripe";

    // Requests use one API version, so an upgrade in the dashboard doesn't change what the app sends
    public const string ApiVersion = "2026-09-30.endive";

    // Stripe's own default: a captured delivery can't be replayed later
    private static readonly TimeSpan SignatureTolerance = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PricesFor = TimeSpan.FromHours(1);
    private const string PricesCacheKey = "billing:stripe:prices";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly StripeSettings _settings;
    private readonly ILogger<StripeBillingProvider> _logger;

    public StripeBillingProvider(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        StripeSettings settings,
        ILogger<StripeBillingProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
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

            // One price per subscription here; since API 2025-03-31 the period lives on the item
            var item = subscription.TryGetProperty("items", out var items)
                       && items.TryGetProperty("data", out var data)
                       && data.ValueKind == JsonValueKind.Array
                       && data.GetArrayLength() > 0
                ? data[0]
                : (JsonElement?)null;

            var priceId = item is { } first && first.TryGetProperty("price", out var price) ? Text(price, "id") : null;

            return new BillingEvent(
                Text(root, "id") ?? throw new JsonException("No event id"),
                type,
                Time(root, "created") ?? throw new JsonException("No event time"),
                Guid.TryParse(Metadata(subscription, "user_id"), out var userId) ? userId : null,
                IdOf(subscription, "customer"),
                Text(subscription, "id"),
                TierOf(priceId),
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

        var session = await SendAsync(HttpMethod.Post, "v1/checkout/sessions", form, "checkout session", cancellationToken);
        return Text(session, "url") ?? throw new BillingUnavailableException("Stripe gave no checkout address");
    }

    public async Task<string> CreatePortalUrlAsync(string customerId, string? language, CancellationToken cancellationToken = default)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("customer", customerId),
            new("return_url", Page("limits")),
            new("locale", LocaleOf(language)),
        };

        var session = await SendAsync(HttpMethod.Post, "v1/billing_portal/sessions", form, "portal session", cancellationToken);
        return Text(session, "url") ?? throw new BillingUnavailableException("Stripe gave no portal address");
    }

    public async Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Delete, $"v1/customers/{Uri.EscapeDataString(customerId)}", null, "customer deletion", cancellationToken, missingIsFine: true);
    }

    public async Task<IReadOnlyList<BillingPrice>> GetPricesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(PricesCacheKey, out IReadOnlyList<BillingPrice>? cached) && cached is not null) return cached;

        var loaded = await Task.WhenAll(_settings.Prices.Select(async pair =>
        {
            var price = await SendAsync(HttpMethod.Get, $"v1/prices/{Uri.EscapeDataString(pair.Value)}", null, "price", cancellationToken);
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

            return new BillingPrice(pair.Key.Plan, pair.Key.Interval, amount.GetInt64(), Text(price, "currency") ?? "eur");
        }));

        var prices = loaded.OfType<BillingPrice>().ToList();
        _cache.Set(PricesCacheKey, (IReadOnlyList<BillingPrice>)prices, PricesFor);
        return prices;
    }

    private async Task<JsonElement> SendAsync(
        HttpMethod method,
        string path,
        List<KeyValuePair<string, string>>? form,
        string what,
        CancellationToken cancellationToken,
        bool missingIsFine = false)
    {
        using var request = new HttpRequestMessage(method, path);
        if (form is not null) request.Content = new FormUrlEncodedContent(form);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);
        request.Headers.Add("Stripe-Version", ApiVersion);

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }

        if (missingIsFine && response.StatusCode == HttpStatusCode.NotFound) return default;

        // Stripe's error code and message help, they never contain the key
        var (code, message) = ErrorOf(body);
        _logger.LogWarning("Stripe answered {Status} to the {What} request: {Code} {Message}",
            (int)response.StatusCode, what, code, message);
        throw new BillingUnavailableException($"Stripe answered {(int)response.StatusCode}");
    }

    private string Page(string relative) => new Uri(_settings.SiteUrl!, relative).ToString();

    private SubscriptionTier? TierOf(string? priceId)
    {
        if (priceId is null) return null;

        foreach (var (key, value) in _settings.Prices)
        {
            if (value == priceId) return key.Plan == BillingPlan.Pro ? SubscriptionTier.Pro : SubscriptionTier.Plus;
        }

        return null;
    }

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

    private static (string? Code, string? Message) ErrorOf(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                ? (Text(error, "code") ?? Text(error, "type"), Text(error, "message"))
                : (null, null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? Metadata(JsonElement element, string key) =>
        element.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object
            ? Text(metadata, key)
            : null;

    // A field that is an id, or the object itself when it was expanded
    private static string? IdOf(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Object => Text(value, "id"),
                _ => null,
            }
            : null;

    private static DateTime? Time(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds)
            ? FromUnix(seconds)
            : null;

    private static DateTime? FromUnix(long seconds) =>
        seconds is >= 0 and <= 253402300799 ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : null;
}
