using Application.DTOs.Admin;
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
}

public interface IAdminStatsRepository
{
    Task<AdminOverviewRaw> GetOverviewAsync(DateTime since, DateTime monthStart, DateTime previousMonthStart);

    Task<List<AiUsageGroupRow>> GetAiUsageAsync(DateTime since);

    Task<List<AiUsageGroupRow>> GetAiUsageTotalsAsync();

    Task<List<AiMonthlyRow>> GetAiMonthlyAsync();
}

public interface IAdminStatsService
{
    Task<AdminOverviewDto> GetOverviewAsync();

    Task<AdminAiStatsDto> GetAiAsync();
}
