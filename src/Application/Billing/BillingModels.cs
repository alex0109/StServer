using System.Security.Cryptography;
using System.Text;
using Domain.Utility.Subscription;

namespace Application.Billing;

// What can be bought; the values are the tiers' values
public enum BillingPlan
{
    Plus = 1,
    Duo = 3,
}

public enum BillingInterval
{
    Month = 1,
    Year = 2,
}

// Everything a provider needs to open its checkout. The user id is set by the server
public record BillingCheckout(
    Guid UserId,
    string? Email,
    BillingPlan Plan,
    BillingInterval Interval,
    // The provider's customer from an earlier plan, so one person stays one customer there
    string? CustomerId,
    string? Language);

// A price as the provider has it; Amount is in the smallest unit of the currency (øre, cents).
// CurrencyAmounts holds the price's own amount in every currency set on it, its main one included
public record BillingPrice(
    BillingPlan Plan,
    BillingInterval Interval,
    long Amount,
    string Currency,
    IReadOnlyDictionary<string, long>? CurrencyAmounts = null);

// The provider's customer of a deleted account, closed after the data is gone
public record BillingCustomerRef(SubscriptionProvider Provider, string CustomerId);

// A provider's webhook in the app's own words. It is what the event log keeps, never the raw body
public record BillingEvent(
    string EventId,
    string Type,
    DateTime OccurredAt,
    // From the checkout's custom data, set by the server and not by the browser
    Guid? UserId,
    string? CustomerId,
    string? SubscriptionId,
    SubscriptionTier? Tier,
    SubscriptionStatus? Status,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    DateTime? TrialEndsAt,
    bool CancelAtPeriodEnd);

public enum WebhookOutcome
{
    Accepted,
    Duplicate,
    Ignored,
    UnknownProvider,
    BadSignature,
}

public static class WebhookSignature
{
    // HMAC-SHA256 of the body as hex, compared in constant time so the answer time leaks nothing
    public static bool IsValidHmacSha256(string secret, ReadOnlySpan<byte> body, string? signatureHex)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrWhiteSpace(signatureHex)) return false;

        byte[] given;
        try
        {
            given = Convert.FromHexString(signatureHex.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }
}

// Money a customer paid for an invoice, tax included, in the invoice's currency (minor units)
public record BillingPayment(DateTime PaidAt, long Amount, string Currency);

// What one movement left on the account after the provider's fees and taxes, refunds as minus,
// in the account's own currency (minor units)
public record BillingBalanceEntry(DateTime At, long Net, string Currency);

// A subscription that will be paid again: its price and when the next payment is due
public record BillingRenewal(Guid? UserId, SubscriptionTier Tier, BillingInterval Interval, long Amount, string Currency, DateTime RenewsAt);

// Webhook types the statistics tell apart. They are the provider's own names: Stripe's, the one in use
public static class BillingEventTypes
{
    public const string Created = "customer.subscription.created";
    public const string Deleted = "customer.subscription.deleted";

    // Credit packs: a paid checkout (cards at once, bank payments later) and a refund
    public const string CheckoutCompleted = "checkout.session.completed";
    public const string CheckoutAsyncSucceeded = "checkout.session.async_payment_succeeded";
    public const string ChargeRefunded = "charge.refunded";

    public static bool IsCredit(string type) => type is CheckoutCompleted or CheckoutAsyncSucceeded or ChargeRefunded;
}

// A tier and the window its AI quotas are counted in, [Start, End)
public record AiPlanPeriod(SubscriptionTier Tier, DateTime Start, DateTime End);

// A credit pack bought at the provider's checkout; credits come from server config, never the client
public record CreditCheckout(
    Guid UserId,
    string? Email,
    string PackId,
    int Credits,
    decimal Price,
    string Currency,
    string? CustomerId,
    string? Language);

public enum CreditEventKind
{
    Purchase = 1,
    Refund = 2,
}

// A paid credit purchase, or a refund of a payment (which may not be a credit purchase at all).
// Reference: the payment both carry; Amount and AmountRefunded: minor units of that payment
public record CreditEvent(
    string EventId,
    string Type,
    DateTime OccurredAt,
    CreditEventKind Kind,
    Guid? UserId,
    string? PackId,
    string? Reference,
    string? SessionId,
    long Amount,
    long AmountRefunded);
