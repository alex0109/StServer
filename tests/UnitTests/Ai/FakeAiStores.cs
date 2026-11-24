using Application.Constants;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Ai;

namespace UnitTests.Ai;

// AI usage and credits kept in lists, filtered the way the database does
internal sealed class FakeAiUsageRepository : IAiUsageRepository
{
    public List<AiUsage> Usages { get; } = [];

    public Task<UserSubscription?> GetSubscriptionAsync(Guid userId) => Task.FromResult<UserSubscription?>(null);

    public Task<UserSubscription?> GetDuoPayerSubscriptionAsync(Guid memberUserId) => Task.FromResult<UserSubscription?>(null);

    public Task<int> SumUnitsAsync(Guid userId, AiFeature feature, AiUsageSource source, DateTime since) =>
        Task.FromResult(Counted(userId, source, since).Where(x => x.Feature == feature).Sum(x => x.Units));

    public Task<Dictionary<AiFeature, int>> SumUnitsByFeatureAsync(Guid userId, AiUsageSource source, DateTime since) =>
        Task.FromResult(Counted(userId, source, since).GroupBy(x => x.Feature).ToDictionary(g => g.Key, g => g.Sum(x => x.Units)));

    public Task<int> CountForgivenAsync(Guid userId, AiFeature feature, DateTime since) =>
        Task.FromResult(Usages.Count(x => x.UserId == userId
                                          && x.Feature == feature
                                          && x.Status == AiUsageStatus.Failed
                                          && x.Provider != null
                                          && x.CreatedAt >= since));

    public Task AddAsync(AiUsage usage)
    {
        Usages.Add(usage);
        return Task.CompletedTask;
    }

    public Task<AiUsage?> GetByIdAsync(Guid usageId) => Task.FromResult(Usages.FirstOrDefault(x => x.Id == usageId));

    public Task<List<Guid>> ListPendingAsync(DateTime createdBefore) =>
        Task.FromResult(Usages.Where(x => x.Status == AiUsageStatus.Pending && x.CreatedAt < createdBefore).Select(x => x.Id).ToList());

    public Task<int> RollUpBeforeAsync(DateTime createdBefore) => Task.FromResult(0);

    public Task SaveChangesAsync() => Task.CompletedTask;

    private IEnumerable<AiUsage> Counted(Guid userId, AiUsageSource source, DateTime since) =>
        Usages.Where(x => x.UserId == userId && x.Source == source && x.CreatedAt >= since && x.Status != AiUsageStatus.Failed);
}

internal sealed class FakeCreditRepository : ICreditRepository
{
    public List<CreditWallet> Wallets { get; } = [];
    public List<CreditTransaction> Transactions { get; } = [];

    public int Balance(Guid userId) => Wallets.FirstOrDefault(x => x.UserId == userId)?.Balance ?? 0;

    public void Give(Guid userId, int credits, bool autoUse = false) =>
        Wallets.Add(new CreditWallet { UserId = userId, Balance = credits, AutoUseForStudy = autoUse, CreatedAt = DateTime.UtcNow });

    public Task<CreditWallet?> GetWalletAsync(Guid userId) => Task.FromResult(Wallets.FirstOrDefault(x => x.UserId == userId));

    public Task AddWalletAsync(CreditWallet wallet)
    {
        Wallets.Add(wallet);
        return Task.CompletedTask;
    }

    public Task<bool> HasTransactionAsync(string idempotencyKey) => Task.FromResult(Transactions.Any(x => x.IdempotencyKey == idempotencyKey));

    public Task AddTransactionAsync(CreditTransaction transaction)
    {
        Transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public Task<CreditTransaction?> FindPurchaseAsync(string stripeReference) =>
        Task.FromResult(Transactions.FirstOrDefault(x => x.StripeReference == stripeReference && x.Reason == CreditReason.Purchase));

    public Task<int> SumRefundedAsync(string stripeReference) =>
        Task.FromResult(-Transactions.Where(x => x.StripeReference == stripeReference && x.Reason == CreditReason.Refund).Sum(x => x.Delta));

    public Task<(List<CreditTransaction> Items, int Total)> ListAsync(Guid userId, int skip, int take)
    {
        var mine = Transactions.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToList();
        return Task.FromResult((mine.Skip(skip).Take(take).ToList(), mine.Count));
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
}

// Serializes like the Postgres advisory lock: one holder per key at a time
internal sealed class FakeLimitLock : ILimitLock
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<ILimitLockScope> AcquireAsync(LimitScope scope, Guid ownerId)
    {
        await _gate.WaitAsync();
        return new Scope(_gate);
    }

    private sealed class Scope(SemaphoreSlim gate) : ILimitLockScope
    {
        private bool _released;

        public Task CommitAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
