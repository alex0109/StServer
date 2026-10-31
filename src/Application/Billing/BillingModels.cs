using System.Security.Cryptography;
using System.Text;
using Domain.Utility.Subscription;

namespace Application.Billing;

public enum BillingPlan
{
    Plus = 1,
    Pro = 2,
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

// A price as the provider has it; Amount is in the smallest unit of the currency (øre, cents)
public record BillingPrice(BillingPlan Plan, BillingInterval Interval, long Amount, string Currency);

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
