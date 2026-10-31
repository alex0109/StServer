namespace Domain.Utility.Subscription;

// Who manages a subscription: the owner by hand, or a Merchant of Record that handles payments and VAT
public enum SubscriptionProvider
{
    Manual = 0,
    Paddle = 1,
    LemonSqueezy = 2,
    // Stripe Billing; with Managed Payments Stripe is the merchant of record too
    Stripe = 3,
}
