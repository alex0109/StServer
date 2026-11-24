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

    public async Task<UserSubscription?> GetDuoPayerSubscriptionAsync(Guid memberUserId)
    {
        return await _db.DuoMemberships.AsNoTracking()
            .Where(x => x.MemberUserId == memberUserId)
            .Join(_db.UserSubscriptions.AsNoTracking(), duo => duo.UserId, subscription => subscription.UserId, (_, subscription) => subscription)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SumUnitsAsync(Guid userId, AiFeature feature, AiUsageSource source, DateTime since)
    {
        return await _db.AiUsages
            .Where(x => x.UserId == userId
                        && x.Feature == feature
                        && x.Source == source
                        && x.CreatedAt >= since
                        && x.Status != AiUsageStatus.Failed)
            .SumAsync(x => x.Units);
    }

    public async Task<Dictionary<AiFeature, int>> SumUnitsByFeatureAsync(Guid userId, AiUsageSource source, DateTime since)
    {
        return await _db.AiUsages
            .Where(x => x.UserId == userId && x.Source == source && x.CreatedAt >= since && x.Status != AiUsageStatus.Failed)
            .GroupBy(x => x.Feature)
            .Select(x => new { Feature = x.Key, Units = x.Sum(u => u.Units) })
            .ToDictionaryAsync(x => x.Feature, x => x.Units);
    }

    // A release without an answer leaves Provider empty; only an answer that came back sets it
    public Task<int> CountForgivenAsync(Guid userId, AiFeature feature, DateTime since) =>
        _db.AiUsages.CountAsync(x => x.UserId == userId
                                     && x.Feature == feature
                                     && x.Status == AiUsageStatus.Failed
                                     && x.Provider != null
                                     && x.CreatedAt >= since);

    public async Task AddAsync(AiUsage usage)
    {
        await _db.AiUsages.AddAsync(usage);
    }

    public async Task<AiUsage?> GetByIdAsync(Guid usageId)
    {
        return await _db.AiUsages.FirstOrDefaultAsync(x => x.Id == usageId);
    }

    public Task<List<Guid>> ListPendingAsync(DateTime createdBefore) =>
        _db.AiUsages
            .Where(x => x.Status == AiUsageStatus.Pending && x.CreatedAt < createdBefore)
            .Select(x => x.Id)
            .ToListAsync();

    public async Task<int> RollUpBeforeAsync(DateTime createdBefore)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        // Trial rows prove the one-time trial is spent, so they stay
        var totals = await _db.AiUsages
            .Where(x => x.CreatedAt < createdBefore && x.Source != AiUsageSource.Trial)
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
            .Where(x => x.CreatedAt < createdBefore && x.Source != AiUsageSource.Trial)
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();

        return removed;
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
