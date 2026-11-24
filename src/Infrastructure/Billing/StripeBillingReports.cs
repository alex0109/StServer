using System.Text.Json;
using Application.Billing;
using Application.Interfaces;
using Domain.Utility.Subscription;
using Microsoft.Extensions.Caching.Memory;
using static Infrastructure.Billing.StripeJson;

namespace Infrastructure.Billing;

// The admin panel's money from Stripe itself: paid invoices for what customers paid, balance
// transactions for what reached the account, and the subscriptions that will renew
public sealed class StripeBillingReports : IBillingReports
{
    // Stripe lists are paged and slow, the panel is opened again and again
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    // Money moved in and out of the account, not money earned
    private static readonly HashSet<string> NotEarnings =
    [
        "payout", "payout_cancel", "payout_failure", "transfer", "transfer_cancel", "transfer_failure",
        "transfer_refund", "topup", "topup_reversal",
    ];

    // A cancelled or ended subscription won't be paid again; a failed payment is still retried
    private static readonly HashSet<string> Renewing = ["active", "trialing", "past_due"];

    private readonly StripeApi _api;
    private readonly IMemoryCache _cache;
    private readonly StripeSettings _settings;

    public StripeBillingReports(StripeApi api, IMemoryCache cache, StripeSettings settings)
    {
        _api = api;
        _cache = cache;
        _settings = settings;
    }

    public bool IsConfigured => _settings.SecretKey is not null;

    public Task<IReadOnlyList<BillingPayment>> GetPaymentsAsync(DateTime since, CancellationToken cancellationToken = default) =>
        CachedAsync($"billing:stripe:payments:{since:yyyyMMdd}", async () =>
        {
            var invoices = await _api.ListAsync("v1/invoices", $"status=paid&created[gte]={ToUnix(since)}", "invoice", cancellationToken);
            return invoices
                .Select(invoice => new BillingPayment(
                    (invoice.TryGetProperty("status_transitions", out var transitions) ? Time(transitions, "paid_at") : null)
                        ?? Time(invoice, "created") ?? since,
                    Number(invoice, "amount_paid") ?? 0,
                    Text(invoice, "currency") ?? "eur"))
                // A trial starts with an invoice of nothing
                .Where(payment => payment.Amount > 0)
                .ToList();
        });

    public Task<IReadOnlyList<BillingBalanceEntry>> GetBalanceAsync(DateTime since, CancellationToken cancellationToken = default) =>
        CachedAsync($"billing:stripe:balance:{since:yyyyMMdd}", async () =>
        {
            var transactions = await _api.ListAsync("v1/balance_transactions", $"created[gte]={ToUnix(since)}", "balance transaction", cancellationToken);
            return transactions
                .Where(transaction => !NotEarnings.Contains(Text(transaction, "type") ?? ""))
                .Select(transaction => new BillingBalanceEntry(
                    Time(transaction, "created") ?? since,
                    Number(transaction, "net") ?? 0,
                    Text(transaction, "currency") ?? "eur"))
                .ToList();
        });

    public Task<IReadOnlyList<BillingRenewal>> GetRenewalsAsync(CancellationToken cancellationToken = default) =>
        CachedAsync("billing:stripe:renewals", async () =>
        {
            var subscriptions = await _api.ListAsync("v1/subscriptions", "status=all", "subscription", cancellationToken);
            return subscriptions.Select(ToRenewal).OfType<BillingRenewal>().ToList();
        });

    private BillingRenewal? ToRenewal(JsonElement subscription)
    {
        if (!Renewing.Contains(Text(subscription, "status") ?? "")) return null;
        if (Flag(subscription, "cancel_at_period_end") || Time(subscription, "cancel_at") is not null) return null;
        if (FirstItem(subscription) is not { } item || !item.TryGetProperty("price", out var price)) return null;
        if (Number(price, "unit_amount") is not { } amount) return null;

        var interval = price.TryGetProperty("recurring", out var recurring) && Text(recurring, "interval") == "year"
            ? BillingInterval.Year
            : BillingInterval.Month;
        var renewsAt = Time(item, "current_period_end") ?? Time(subscription, "current_period_end");
        if (renewsAt is null) return null;

        return new BillingRenewal(
            Guid.TryParse(Metadata(subscription, "user_id"), out var userId) ? userId : null,
            _settings.TierOf(Text(price, "id")) ?? SubscriptionTier.Plus,
            interval,
            amount * (Number(item, "quantity") ?? 1),
            Text(price, "currency") ?? "eur",
            renewsAt.Value);
    }

    private async Task<IReadOnlyList<T>> CachedAsync<T>(string key, Func<Task<List<T>>> load)
    {
        if (_cache.TryGetValue(key, out IReadOnlyList<T>? cached) && cached is not null) return cached;

        IReadOnlyList<T> loaded = await load();
        _cache.Set(key, loaded, CacheFor);
        return loaded;
    }
}
