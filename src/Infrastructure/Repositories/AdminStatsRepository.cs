using Application.DTOs.Admin;
using Application.Interfaces;
using Domain.Utility.Ai;
using Domain.Utility.Attempt;
using Domain.Utility.Library;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

// Read-only counts for the admin panel. Everything is grouped by the database; only small
// per-user and per-day lists come back
public class AdminStatsRepository : IAdminStatsRepository
{
    private readonly AppDbContext _db;

    public AdminStatsRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminOverviewRaw> GetOverviewAsync(DateTime since, DateTime monthStart, DateTime previousMonthStart)
    {
        // A user is first seen at the earliest trace they left anywhere
        var firstSeen = new Dictionary<Guid, DateTime>();
        void Merge(IEnumerable<(Guid UserId, DateTime At)> rows)
        {
            foreach (var (userId, at) in rows)
                firstSeen[userId] = firstSeen.TryGetValue(userId, out var known) && known <= at ? known : at;
        }

        Merge((await _db.Materials.GroupBy(x => x.UserId).Select(g => new { g.Key, At = g.Min(x => x.CreatedAt) }).ToListAsync()).Select(x => (x.Key, x.At)));
        Merge((await _db.UserNotificationSettings.Select(x => new { x.UserId, x.CreatedAt }).ToListAsync()).Select(x => (x.UserId, x.CreatedAt)));
        Merge((await _db.AiUsages.GroupBy(x => x.UserId).Select(g => new { g.Key, At = g.Min(x => x.CreatedAt) }).ToListAsync()).Select(x => (x.Key, x.At)));
        Merge((await _db.AiUsageMonthly.GroupBy(x => x.UserId).Select(g => new { g.Key, Month = g.Min(x => x.Year * 12 + x.Month - 1) }).ToListAsync())
            .Select(x => (x.Key, new DateTime(x.Month / 12, x.Month % 12 + 1, 1, 0, 0, 0, DateTimeKind.Utc))));
        Merge((await _db.Attempts.GroupBy(x => x.UserId).Select(g => new { g.Key, At = g.Min(x => x.StartedAt) }).ToListAsync()).Select(x => (x.Key, x.At)));
        Merge((await _db.FlashcardReviews.GroupBy(x => x.UserId).Select(g => new { g.Key, At = g.Min(x => x.ReviewedAt) }).ToListAsync()).Select(x => (x.Key, x.At)));
        Merge((await _db.LibraryRequests.GroupBy(x => x.UserId).Select(g => new { g.Key, At = g.Min(x => x.CreatedAt) }).ToListAsync()).Select(x => (x.Key, x.At)));
        Merge((await _db.UserSubscriptions.Select(x => new { x.UserId, x.CreatedAt }).ToListAsync()).Select(x => (x.UserId, x.CreatedAt)));

        // Active means studied, used AI or wrote something that day
        var active = new HashSet<(Guid, DateOnly)>();
        void Add(IEnumerable<(Guid UserId, DateTime Day)> rows)
        {
            foreach (var (userId, day) in rows) active.Add((userId, DateOnly.FromDateTime(day)));
        }

        Add((await _db.Attempts.Where(x => x.StartedAt >= since).Select(x => new { x.UserId, Day = x.StartedAt.Date }).Distinct().ToListAsync()).Select(x => (x.UserId, x.Day)));
        Add((await _db.FlashcardReviews.Where(x => x.ReviewedAt >= since).Select(x => new { x.UserId, Day = x.ReviewedAt.Date }).Distinct().ToListAsync()).Select(x => (x.UserId, x.Day)));
        Add((await _db.AiUsages.Where(x => x.CreatedAt >= since).Select(x => new { x.UserId, Day = x.CreatedAt.Date }).Distinct().ToListAsync()).Select(x => (x.UserId, x.Day)));
        Add((await _db.Materials.Where(x => x.UpdatedAt >= since).Select(x => new { x.UserId, Day = x.UpdatedAt.Date }).Distinct().ToListAsync()).Select(x => (x.UserId, x.Day)));
        Add((await _db.Notes.Where(x => x.UpdatedAt >= since).Select(x => new { x.UserId, Day = x.UpdatedAt.Date }).Distinct().ToListAsync()).Select(x => (x.UserId, x.Day)));

        var content = new AdminContentDto(
            Materials: await _db.Materials.CountAsync(),
            Notes: await _db.Notes.CountAsync(),
            Questions: await _db.Questions.CountAsync(x => x.IsActive && x.DeletedAt == null),
            Dictionaries: await _db.Dictionaries.CountAsync(),
            Words: await _db.DictionaryEntries.CountAsync(),
            Tags: await _db.Tags.CountAsync(),
            FinishedAttempts: await _db.Attempts.CountAsync(x => x.AttemptStatus == AttemptStatus.Finished),
            FlashcardReviews: await _db.FlashcardReviews.CountAsync(),
            ActiveShares: await _db.MaterialShares.CountAsync(x => x.RevokedAt == null));

        var library = new AdminLibraryDto(
            Published: await _db.LibraryItems.CountAsync(x => x.Status == LibraryItemStatus.Published),
            PendingRequests: await _db.LibraryRequests.CountAsync(x => x.Status == LibraryRequestStatus.Pending),
            Imports: await _db.LibraryItems.SumAsync(x => (int?)x.ImportsCount) ?? 0,
            RequestsLast30Days: await _db.LibraryRequests.CountAsync(x => x.CreatedAt >= since));

        var feedback = new AdminFeedbackDto(
            ThisMonth: await _db.FeedbackSubmissions.CountAsync(x => x.CreatedAt >= monthStart),
            LastMonth: await _db.FeedbackSubmissions.CountAsync(x => x.CreatedAt >= previousMonthStart && x.CreatedAt < monthStart));

        return new AdminOverviewRaw(
            firstSeen,
            active.Select(x => (x.Item1, x.Item2)).ToList(),
            content,
            library,
            feedback,
            await ByDayAsync(_db.Attempts.Where(x => x.AttemptStatus == AttemptStatus.Finished && x.FinishedAt >= since).Select(x => x.FinishedAt!.Value)),
            await ByDayAsync(_db.FlashcardReviews.Where(x => x.ReviewedAt >= since).Select(x => x.ReviewedAt)),
            await ByDayAsync(_db.Materials.Where(x => x.CreatedAt >= since).Select(x => x.CreatedAt)),
            await ByDayAsync(_db.AiUsages.Where(x => x.CreatedAt >= since && x.Status != AiUsageStatus.Pending).Select(x => x.CreatedAt)));
    }

    public async Task<List<AiUsageGroupRow>> GetAiUsageAsync(DateTime since)
    {
        var rows = await _db.AiUsages
            .Where(x => x.CreatedAt >= since)
            .GroupBy(x => new { Day = x.CreatedAt.Date, x.Feature, x.Provider, x.Model, x.UserId, x.Status })
            .Select(g => new
            {
                g.Key.Day, g.Key.Feature, g.Key.Provider, g.Key.Model, g.Key.UserId, g.Key.Status,
                Requests = g.Count(),
                InputTokens = g.Sum(x => x.InputTokens),
                OutputTokens = g.Sum(x => x.OutputTokens),
            })
            .ToListAsync();

        return rows
            .Select(x => new AiUsageGroupRow(DateOnly.FromDateTime(x.Day), x.Feature, x.Provider, x.Model, x.UserId, x.Status, x.Requests, x.InputTokens, x.OutputTokens))
            .ToList();
    }

    public async Task<List<AiUsageGroupRow>> GetAiUsageTotalsAsync()
    {
        var rows = await _db.AiUsages
            .GroupBy(x => new { x.Feature, x.Provider, x.Model, x.Status })
            .Select(g => new
            {
                g.Key.Feature, g.Key.Provider, g.Key.Model, g.Key.Status,
                Requests = g.Count(),
                InputTokens = g.Sum(x => x.InputTokens),
                OutputTokens = g.Sum(x => x.OutputTokens),
            })
            .ToListAsync();

        return rows
            .Select(x => new AiUsageGroupRow(default, x.Feature, x.Provider, x.Model, Guid.Empty, x.Status, x.Requests, x.InputTokens, x.OutputTokens))
            .ToList();
    }

    public async Task<List<AiMonthlyRow>> GetAiMonthlyAsync()
    {
        var rows = await _db.AiUsageMonthly
            .GroupBy(x => new { x.Provider, x.Model })
            .Select(g => new
            {
                g.Key.Provider, g.Key.Model,
                Requests = g.Sum(x => x.Requests),
                Failed = g.Sum(x => x.FailedRequests),
                InputTokens = g.Sum(x => x.InputTokens),
                OutputTokens = g.Sum(x => x.OutputTokens),
            })
            .ToListAsync();

        return rows.Select(x => new AiMonthlyRow(x.Provider, x.Model, x.Requests, x.Failed, x.InputTokens, x.OutputTokens)).ToList();
    }

    private static async Task<Dictionary<DateOnly, int>> ByDayAsync(IQueryable<DateTime> moments)
    {
        var rows = await moments.GroupBy(x => x.Date).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync();
        return rows.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count);
    }
}
