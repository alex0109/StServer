using Application.Billing;

namespace Application.Interfaces;

// Money as the billing provider sees it, for the admin panel; nothing here changes a subscription
public interface IBillingReports
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<BillingPayment>> GetPaymentsAsync(DateTime since, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingBalanceEntry>> GetBalanceAsync(DateTime since, CancellationToken cancellationToken = default);

    // Subscriptions that renew: not cancelled, not ended
    Task<IReadOnlyList<BillingRenewal>> GetRenewalsAsync(CancellationToken cancellationToken = default);
}
