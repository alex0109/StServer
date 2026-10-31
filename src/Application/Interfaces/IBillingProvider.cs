using Application.Billing;
using Domain.Utility.Subscription;

namespace Application.Interfaces;

// One payment provider (Stripe). Secrets come only from environment variables
public interface IBillingProvider
{
    SubscriptionProvider Provider { get; }

    // The provider's part of the webhook path: /api/billing/webhook/{name}
    string Name { get; }

    // Header that carries the webhook signature
    string SignatureHeader { get; }

    bool IsConfigured { get; }

    bool VerifyWebhook(string? signatureHeader, byte[] rawBody, DateTime now);

    // Null for events the app doesn't use
    BillingEvent? ParseEvent(byte[] rawBody);

    // The user id goes to the provider as metadata and comes back in every webhook
    Task<string> CreateCheckoutUrlAsync(BillingCheckout checkout, CancellationToken cancellationToken = default);

    // The provider's own page for cards, invoices, plan changes and cancelling
    Task<string> CreatePortalUrlAsync(string customerId, string? language, CancellationToken cancellationToken = default);

    // Closes the customer and its subscriptions; a customer that is already gone is fine
    Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default);

    // Configured prices with their amounts, for the plan cards
    Task<IReadOnlyList<BillingPrice>> GetPricesAsync(CancellationToken cancellationToken = default);
}
