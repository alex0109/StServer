using Application.Interfaces;

namespace Application.Jobs;

// A stored webhook is applied here, away from the request: the provider gets its 200 at once,
// and a failure is retried by Hangfire
public class BillingEventJob
{
    private readonly IBillingService _billing;

    public BillingEventJob(IBillingService billing)
    {
        _billing = billing;
    }

    public Task Run(Guid eventId) => _billing.ProcessEventAsync(eventId);
}
