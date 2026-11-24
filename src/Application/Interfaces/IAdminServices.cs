using Application.DTOs.Admin;
using Domain.Entities;
using Domain.Utility.Admin;

namespace Application.Interfaces;

// Append-only log of what admins did
public interface IAdminAudit
{
    Task RecordAsync(AdminAction action, Guid? targetId = null, string? details = null);

    Task<List<AdminAuditEntryDto>> ListAsync(int take);
}

// Emails to the owner about the admin panel: every sign-in, and every refused attempt
public interface IAdminAlerts
{
    // Once per session: a repeated call for the same session sends nothing
    Task SignedInAsync(string? sessionId);

    Task AccessDeniedAsync(string reason);

    // A plan given or taken back by hand
    Task SubscriptionChangedAsync(string summary);
}

public interface IAdminStatsRepository
{
    Task<AdminOverviewRaw> GetOverviewAsync(DateTime since, DateTime monthStart, DateTime previousMonthStart);

    Task<List<AiUsageGroupRow>> GetAiUsageAsync(DateTime since);

    Task<List<AiUsageGroupRow>> GetAiUsageTotalsAsync();

    Task<List<AiMonthlyRow>> GetAiMonthlyAsync();

    Task<int> CountManualAcceptsAsync(DateTime since);

    // Generating rows last touched before staleBefore count as failed
    Task<AdminPodcastRaw> GetPodcastsAsync(DateTime staleBefore, int take);

    Task<List<SpeechUsageGroupRow>> GetSpeechUsageAsync(DateTime since);

    Task<List<SpeechUsageGroupRow>> GetSpeechUsageTotalsAsync();

    // Succeeded AI requests since a moment, grouped by feature and fallback
    Task<List<AiCostRow>> GetAiCostsAsync(DateTime since);

    // AI cost per user since a moment, voices of podcasts included, most expensive first
    Task<List<AiUserCostRow>> GetUserCostsAsync(DateTime since, decimal speechPerMillion, int take);

    // Credits bought and spent since a moment
    Task<(int Sold, int Spent)> GetCreditFlowAsync(DateTime since);
}

// Read-only subscription data for the admin panel's billing page
public interface IAdminBillingRepository
{
    // Every subscription row, ended ones too: there are only as many as users who ever had a plan
    Task<List<UserSubscription>> ListSubscriptionsAsync();

    // Payers whose Duo has a second person in it
    Task<List<Guid>> ListDuoPayersWithMemberAsync();

    // Provider webhooks since a moment, oldest first
    Task<List<SubscriptionEventRow>> ListSubscriptionEventsAsync(DateTime since);
}

public interface IAdminStatsService
{
    Task<AdminOverviewDto> GetOverviewAsync();

    Task<AdminAiStatsDto> GetAiAsync();

    Task<AdminPodcastStatsDto> GetPodcastsAsync();

    Task<AdminAiMarginsDto> GetAiMarginsAsync();
}

public interface IAdminBillingStatsService
{
    Task<AdminBillingStatsDto> GetAsync(CancellationToken cancellationToken = default);
}
