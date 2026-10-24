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

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
