using Application.Billing;
using Application.DTOs.Billing;

namespace Application.Interfaces;

public interface IBillingService
{
    // Checks, stores and queues a webhook; the change itself is made in the background
    Task<WebhookOutcome> ReceiveWebhookAsync(string provider, string? signature, byte[] body);

    // Applies a stored event once; safe to run again
    Task ProcessEventAsync(Guid eventId);

    Task<string> CreateCheckoutAsync(BillingPlan plan, BillingInterval interval, string? language);

    // The provider's page where a paid plan is managed
    Task<string> CreatePortalAsync(string? language);

    Task<BillingStatusDto> GetStatusAsync();

    // Header name a provider signs with, null for an unknown provider
    string? SignatureHeaderOf(string provider);
}

// Plans with prices and what each tier gives; public, so it holds nothing about a user
public interface IBillingCatalog
{
    Task<BillingPlansDto> GetPlansAsync(CancellationToken cancellationToken = default);
}

// Background work for billing, Hangfire in the app
public interface IBillingJobs
{
    void ProcessEvent(Guid eventId);
}

public interface ISubscriptionAdminService
{
    Task<List<AdminSubscriptionDto>> ListAsync(int take);
    Task<AdminSubscriptionDto> GrantAsync(SubscriptionGrantDto dto);
    // Only hand-made grants; false when there is none
    Task<bool> RevokeAsync(Guid userId);
}
