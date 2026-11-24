namespace Domain.Utility.Ai;

public enum CreditReason
{
    // A pack bought through Stripe
    Purchase = 1,
    // Taken by an AI request
    Spend = 2,
    // Given back: the request that took them failed
    Return = 3,
    // Taken back after the purchase was refunded in Stripe
    Refund = 4,
    // By hand
    Adjustment = 5,
}
