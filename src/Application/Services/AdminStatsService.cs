using Application.Admin;
using Application.DTOs.Admin;
using Application.Interfaces;
using Domain.Utility.Ai;

namespace Application.Services;

// Numbers for the admin panel. Days are UTC days; "last N days" includes today
public class AdminStatsService : IAdminStatsService
{
    private const int Window = 30;

    private readonly IAdminStatsRepository _repo;
    private readonly AiPricingOptions _pricing;
    private readonly TimeProvider _time;

    public AdminStatsService(IAdminStatsRepository repo, AiPricingOptions pricing, TimeProvider time)
    {
        _repo = repo;
        _pricing = pricing;
        _time = time;
    }

    public async Task<AdminOverviewDto> GetOverviewAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var firstDay = today.AddDays(-(Window - 1));
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var raw = await _repo.GetOverviewAsync(StartOf(firstDay), monthStart, monthStart.AddMonths(-1));

        var firstSeen = raw.FirstSeen.Values.Select(DateOnly.FromDateTime).ToList();
        int ActiveSince(DateOnly from) => raw.ActiveDays.Where(x => x.Day >= from).Select(x => x.UserId).Distinct().Count();
        int NewSince(DateOnly from) => firstSeen.Count(day => day >= from);

        var activeByDay = raw.ActiveDays.GroupBy(x => x.Day).ToDictionary(g => g.Key, g => g.Select(x => x.UserId).Distinct().Count());
        var newByDay = firstSeen.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var days = Enumerable.Range(0, Window)
            .Select(i => firstDay.AddDays(i))
            .Select(day => new AdminDayDto(
                day,
                activeByDay.GetValueOrDefault(day),
                newByDay.GetValueOrDefault(day),
                raw.FinishedAttemptsByDay.GetValueOrDefault(day),
                raw.ReviewsByDay.GetValueOrDefault(day),
                raw.NewMaterialsByDay.GetValueOrDefault(day),
                raw.AiRequestsByDay.GetValueOrDefault(day)))
            .ToList();

        return new AdminOverviewDto(
            new AdminUsersDto(
                Total: raw.FirstSeen.Count,
                ActiveToday: ActiveSince(today),
                Active7Days: ActiveSince(today.AddDays(-6)),
                Active30Days: ActiveSince(firstDay),
                New7Days: NewSince(today.AddDays(-6)),
                New30Days: NewSince(firstDay)),
            raw.Content,
            raw.Library,
            raw.Feedback,
            days,
            now);
    }

    public async Task<AdminAiStatsDto> GetAiAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var firstDay = today.AddDays(-(Window - 1));
        var monthStart = DateOnly.FromDateTime(new DateTime(now.Year, now.Month, 1));

        // The window and the calendar month, whichever starts earlier; pending requests are still running
        var from = firstDay < monthStart ? firstDay : monthStart;
        var rows = (await _repo.GetAiUsageAsync(StartOf(from)))
            .Where(x => x.Status != AiUsageStatus.Pending)
            .Select(x => new Priced(x, _pricing.Cost(x.Model, x.InputTokens, x.OutputTokens)))
            .ToList();

        var window = rows.Where(x => x.Row.Date >= firstDay).ToList();
        var month = rows.Where(x => x.Row.Date >= monthStart).ToList();
        var last7 = window.Where(x => x.Row.Date >= today.AddDays(-6)).ToList();
        var todays = window.Where(x => x.Row.Date == today).ToList();

        var allTime = (await _repo.GetAiUsageTotalsAsync())
            .Where(x => x.Status != AiUsageStatus.Pending)
            .Select(x => new Priced(x, _pricing.Cost(x.Model, x.InputTokens, x.OutputTokens)))
            .ToList();
        var rolledUp = await _repo.GetAiMonthlyAsync();

        var requests30 = Requests(window);
        var aiUsers30 = window.Select(x => x.Row.UserId).Distinct().Count();

        var totals = new AdminAiTotalsDto(
            AllTime: Requests(allTime) + rolledUp.Sum(x => (long)x.Requests + x.Failed),
            Today: Requests(todays),
            Last7Days: Requests(last7),
            Last30Days: requests30,
            ThisMonth: Requests(month),
            FailedLast30Days: Failed(window),
            AveragePerDay30: Math.Round(requests30 / (double)Window, 1),
            AveragePerActiveUser30: aiUsers30 == 0 ? 0 : Math.Round(requests30 / (double)aiUsers30, 1),
            AiUsers30: aiUsers30);

        var tokens = new AdminAiTokensDto(
            window.Sum(x => x.Row.InputTokens), window.Sum(x => x.Row.OutputTokens),
            month.Sum(x => x.Row.InputTokens), month.Sum(x => x.Row.OutputTokens));

        var cost30 = Cost(window);
        var cost = new AdminAiCostDto(
            Today: Cost(todays),
            Last7Days: Cost(last7),
            Last30Days: cost30,
            ThisMonth: Cost(month),
            AllTime: Cost(allTime) + rolledUp.Sum(x => _pricing.Cost(x.Model, x.InputTokens, x.OutputTokens) ?? 0),
            AveragePerRequest30: requests30 == 0 ? 0 : Math.Round(cost30 / requests30, 6),
            // Failed requests without tokens cost nothing, a missing price doesn't matter for them
            UnpricedRequests30: window.Where(x => x.Cost is null && x.Row.InputTokens + x.Row.OutputTokens > 0).Sum(x => x.Row.Requests),
            PricesCheckedOn: _pricing.CheckedOn);

        var byFeature = window
            .GroupBy(x => x.Row.Feature)
            .Select(g => new AdminAiFeatureDto(g.Key, Requests(g), Failed(g), Cost(g)))
            .OrderByDescending(x => x.Requests)
            .ToList();

        var byModel = window
            .GroupBy(x => (Provider: x.Row.Provider ?? "", Model: x.Row.Model ?? ""))
            .Select(g => new AdminAiModelDto(
                g.Key.Provider, g.Key.Model, Requests(g), Failed(g),
                g.Sum(x => x.Row.InputTokens), g.Sum(x => x.Row.OutputTokens),
                g.Any(x => x.Cost is null && x.Row.InputTokens + x.Row.OutputTokens > 0) ? null : Cost(g)))
            .OrderByDescending(x => x.Requests)
            .ToList();

        var byDay = window.GroupBy(x => x.Row.Date).ToDictionary(g => g.Key, g => g.ToList());
        var days = Enumerable.Range(0, Window)
            .Select(i => firstDay.AddDays(i))
            .Select(day => byDay.TryGetValue(day, out var list)
                ? new AdminAiDayDto(day, Requests(list), Failed(list), Cost(list))
                : new AdminAiDayDto(day, 0, 0, 0))
            .ToList();

        var topUsers = window
            .GroupBy(x => x.Row.UserId)
            .Select(g => new AdminAiUserDto(g.Key, Requests(g), Cost(g)))
            .OrderByDescending(x => x.Requests)
            .Take(10)
            .ToList();

        return new AdminAiStatsDto(totals, tokens, cost, byFeature, byModel, days, topUsers, now);
    }

    private sealed record Priced(AiUsageGroupRow Row, decimal? Cost);

    private static int Requests(IEnumerable<Priced> rows) => rows.Sum(x => x.Row.Requests);

    private static int Failed(IEnumerable<Priced> rows) =>
        rows.Where(x => x.Row.Status == AiUsageStatus.Failed).Sum(x => x.Row.Requests);

    private static decimal Cost(IEnumerable<Priced> rows) => Math.Round(rows.Sum(x => x.Cost ?? 0), 4);

    private static DateTime StartOf(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
