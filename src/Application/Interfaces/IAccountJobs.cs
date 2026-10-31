using Domain.Utility.Subscription;

namespace Application.Interfaces;

// Work after the data is gone that may need retries, so it runs in the background
public interface IAccountJobs
{
    void DeleteAuthUser(Guid userId);

    // The customer of a paid plan at the payment provider, with its subscriptions
    void DeleteBillingCustomer(SubscriptionProvider provider, string customerId);
}
