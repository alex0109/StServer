using Application.Interfaces;
using Application.Jobs;
using Domain.Utility.Subscription;
using Hangfire;

namespace Api.Common;

// Hangfire keeps only the user id; failed runs are retried with growing delays (its default, 10 times)
public sealed class HangfireAccountJobs : IAccountJobs
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireAccountJobs(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public void DeleteAuthUser(Guid userId) => _jobs.Enqueue<DeleteAuthUserJob>(x => x.Run(userId));

    public void DeleteBillingCustomer(SubscriptionProvider provider, string customerId) =>
        _jobs.Enqueue<DeleteBillingCustomerJob>(x => x.Run(provider, customerId));
}

public sealed class HangfireBillingJobs : IBillingJobs
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireBillingJobs(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public void ProcessEvent(Guid eventId) => _jobs.Enqueue<BillingEventJob>(x => x.Run(eventId));
}

// Tests run without Hangfire: nothing to delete outside the database there
public sealed class NoAccountJobs : IAccountJobs
{
    public void DeleteAuthUser(Guid userId)
    {
    }

    public void DeleteBillingCustomer(SubscriptionProvider provider, string customerId)
    {
    }
}

// Tests run without Hangfire: events wait in the log, a test applies them itself
public sealed class NoBillingJobs : IBillingJobs
{
    public void ProcessEvent(Guid eventId)
    {
    }
}
