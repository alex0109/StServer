using Application.Admin;
using Application.DTOs.Admin;
using Application.Interfaces;
using Application.Podcasts;
using Domain.Utility.Ai;

namespace Application.Services;

// Numbers for the admin panel. Days are UTC days; "last N days" includes today
public class AdminStatsService : IAdminStatsService
{
    private const int Window = 30;
    // Rows in the lists of recent and failed podcasts
    private const int PodcastListSize = 10;

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
            AiUsers30: aiUsers30,
            ManualAccepts30: await _repo.CountManualAcceptsAsync(StartOf(firstDay)));

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

    public async Task<AdminPodcastStatsDto> GetPodcastsAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var firstDay = today.AddDays(-(Window - 1));
        var monthStart = DateOnly.FromDateTime(new DateTime(now.Year, now.Month, 1));
        var from = firstDay < monthStart ? firstDay : monthStart;

        var raw = await _repo.GetPodcastsAsync(now - PodcastRules.StaleAfter, PodcastListSize);
        var speech = await _repo.GetSpeechUsageAsync(StartOf(from));
        var speechTotals = await _repo.GetSpeechUsageTotalsAsync();
        // Scripts are written by the AI and logged with its other requests
        var scripts = (await _repo.GetAiUsageAsync(StartOf(from)))
            .Where(x => x.Feature == AiFeature.PodcastGeneration && x.Status != AiUsageStatus.Pending)
            .ToList();

        var window = speech.Where(x => x.Date >= firstDay).ToList();
        var month = speech.Where(x => x.Date >= monthStart).ToList();

        var recordings = new AdminPodcastRecordingsDto(
            AllTime: Recordings(speechTotals),
            Today: Recordings(window.Where(x => x.Date == today)),
            Last7Days: Recordings(window.Where(x => x.Date >= today.AddDays(-6))),
            Last30Days: Recordings(window),
            ThisMonth: Recordings(month),
            FailedLast30Days: Recordings(window.Where(x => !x.Succeeded)),
            CharactersLast30Days: window.Sum(x => x.Characters),
            CharactersThisMonth: month.Sum(x => x.Characters),
            CharactersAllTime: speechTotals.Sum(x => x.Characters));

        var voices30 = VoiceCost(recordings.CharactersLast30Days);
        var scripts30 = ScriptCost(scripts.Where(x => x.Date >= firstDay));
        var succeeded30 = Recordings(window.Where(x => x.Succeeded));

        var cost = new AdminPodcastCostDto(
            VoicesLast30Days: voices30,
            VoicesThisMonth: VoiceCost(recordings.CharactersThisMonth),
            VoicesAllTime: VoiceCost(recordings.CharactersAllTime),
            ScriptsLast30Days: scripts30,
            ScriptsThisMonth: ScriptCost(scripts.Where(x => x.Date >= monthStart)),
            AveragePerPodcast30: voices30 is { } voices && succeeded30 > 0 ? Math.Round((voices + scripts30) / succeeded30, 4) : null,
            SpeechPerMillionCharacters: _pricing.SpeechPerMillionCharacters);

        var byDay = window.GroupBy(x => x.Date).ToDictionary(g => g.Key, g => g.ToList());
        var days = Enumerable.Range(0, Window)
            .Select(i => firstDay.AddDays(i))
            .Select(day =>
            {
                var rows = byDay.GetValueOrDefault(day) ?? [];
                var characters = rows.Sum(x => x.Characters);
                return new AdminPodcastDayDto(day, Recordings(rows), Recordings(rows.Where(x => !x.Succeeded)), characters, VoiceCost(characters) ?? 0);
            })
            .ToList();

        var library = new AdminPodcastLibraryDto(
            Total: raw.Ready + raw.Generating + raw.Failed,
            Ready: raw.Ready,
            Generating: raw.Generating,
            Failed: raw.Failed,
            ReadySeconds: Math.Round(raw.ReadySeconds),
            AverageSeconds: raw.Ready == 0 ? 0 : Math.Round(raw.ReadySeconds / raw.Ready),
            StorageBytes: raw.StorageBytes);

        // A hung recording is shown the way its user sees it: failed
        AdminPodcastItemDto AsSeen(AdminPodcastItemDto item) =>
            item with { Status = PodcastRules.StatusAt(item.Status, item.UpdatedAt, now) };

        return new AdminPodcastStatsDto(
            library, recordings, cost, days, raw.Languages,
            raw.Recent.Select(AsSeen).ToList(),
            raw.Failures.Select(AsSeen).ToList(),
            now);
    }

    private decimal? VoiceCost(long characters) =>
        _pricing.SpeechCost(characters) is { } cost ? Math.Round(cost, 4) : null;

    private decimal ScriptCost(IEnumerable<AiUsageGroupRow> rows) =>
        Math.Round(rows.Sum(x => _pricing.Cost(x.Model, x.InputTokens, x.OutputTokens) ?? 0), 4);

    private static int Recordings(IEnumerable<SpeechUsageGroupRow> rows) => rows.Sum(x => x.Recordings);

    private sealed record Priced(AiUsageGroupRow Row, decimal? Cost);

    private static int Requests(IEnumerable<Priced> rows) => rows.Sum(x => x.Row.Requests);

    private static int Failed(IEnumerable<Priced> rows) =>
        rows.Where(x => x.Row.Status == AiUsageStatus.Failed).Sum(x => x.Row.Requests);

    private static decimal Cost(IEnumerable<Priced> rows) => Math.Round(rows.Sum(x => x.Cost ?? 0), 4);

    private static DateTime StartOf(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
