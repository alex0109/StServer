namespace Domain.Utility.Subscription;

public enum SubscriptionStatus
{
    Active = 0,
    Trialing = 1,
    // A payment failed and the provider retries; the plan stays for a short grace period
    PastDue = 2,
    // Will not renew; the plan stays until the paid period ends
    Canceled = 3,
    Expired = 4,
}
