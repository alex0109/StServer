using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Ai;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class AiUsageRepository : IAiUsageRepository
{
    private readonly AppDbContext _db;

    public AiUsageRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UserSubscription?> GetSubscriptionAsync(Guid userId)
    {
        return await _db.UserSubscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task<int> CountAsync(Guid userId, AiFeature feature, DateTime since)
    {
        return await _db.AiUsages.CountAsync(x =>
            x.UserId == userId
            && x.Feature == feature
            && x.CreatedAt >= since
            && x.Status != AiUsageStatus.Failed);
    }

    public async Task<Dictionary<AiFeature, int>> CountByFeatureAsync(Guid userId, DateTime since)
    {
        return await _db.AiUsages
            .Where(x => x.UserId == userId && x.CreatedAt >= since && x.Status != AiUsageStatus.Failed)
            .GroupBy(x => x.Feature)
            .Select(x => new { Feature = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Feature, x => x.Count);
    }

    public async Task AddAsync(AiUsage usage)
    {
        await _db.AiUsages.AddAsync(usage);
    }

    public async Task<AiUsage?> GetByIdAsync(Guid usageId)
    {
        return await _db.AiUsages.FirstOrDefaultAsync(x => x.Id == usageId);
    }

    public async Task<int> FailPendingAsync(DateTime createdBefore, DateTime now)
    {
        return await _db.AiUsages
            .Where(x => x.Status == AiUsageStatus.Pending && x.CreatedAt < createdBefore)
            .ExecuteUpdateAsync(x => x
                .SetProperty(u => u.Status, AiUsageStatus.Failed)
                .SetProperty(u => u.CompletedAt, now));
    }

    public async Task<int> RollUpBeforeAsync(DateTime createdBefore)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var totals = await _db.AiUsages
            .Where(x => x.CreatedAt < createdBefore)
            .GroupBy(x => new
            {
                x.UserId,
                x.CreatedAt.Year,
                x.CreatedAt.Month,
                x.Feature,
                Provider = x.Provider ?? "",
                Model = x.Model ?? "",
            })
            .Select(g => new
            {
                g.Key,
                Requests = g.Count(x => x.Status == AiUsageStatus.Succeeded),
                Failed = g.Count(x => x.Status != AiUsageStatus.Succeeded),
                InputTokens = g.Sum(x => x.InputTokens),
                OutputTokens = g.Sum(x => x.OutputTokens),
            })
            .ToListAsync();

        if (totals.Count == 0) return 0;

        foreach (var total in totals)
        {
            var key = total.Key;
            var monthly = await _db.AiUsageMonthly.FirstOrDefaultAsync(x =>
                x.UserId == key.UserId && x.Year == key.Year && x.Month == key.Month
                && x.Feature == key.Feature && x.Provider == key.Provider && x.Model == key.Model);

            if (monthly is null)
            {
                monthly = new AiUsageMonthly
                {
                    UserId = key.UserId,
                    Year = key.Year,
                    Month = key.Month,
                    Feature = key.Feature,
                    Provider = key.Provider,
                    Model = key.Model,
                };
                await _db.AiUsageMonthly.AddAsync(monthly);
            }

            monthly.Requests += total.Requests;
            monthly.FailedRequests += total.Failed;
            monthly.InputTokens += total.InputTokens;
            monthly.OutputTokens += total.OutputTokens;
        }

        await _db.SaveChangesAsync();

        var removed = await _db.AiUsages
            .Where(x => x.CreatedAt < createdBefore)
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();

        return removed;
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
