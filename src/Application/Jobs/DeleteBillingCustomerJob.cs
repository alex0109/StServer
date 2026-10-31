using Application.Interfaces;
using Domain.Utility.Subscription;
using Microsoft.Extensions.Logging;

namespace Application.Jobs;

// After an account is deleted its paid plan must stop charging: the provider's customer is closed,
// which cancels its subscriptions. A failure throws, so Hangfire retries with growing delays
public class DeleteBillingCustomerJob
{
    private readonly IEnumerable<IBillingProvider> _providers;
    private readonly ILogger<DeleteBillingCustomerJob> _logger;

    public DeleteBillingCustomerJob(IEnumerable<IBillingProvider> providers, ILogger<DeleteBillingCustomerJob> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public async Task Run(SubscriptionProvider provider, string customerId)
    {
        var billing = _providers.FirstOrDefault(x => x.Provider == provider && x.IsConfigured)
                      ?? throw new InvalidOperationException($"{provider} is not configured, its customer can't be closed");

        await billing.DeleteCustomerAsync(customerId);
        _logger.LogInformation("Billing customer of a deleted account closed at {Provider}", billing.Name);
    }
}
