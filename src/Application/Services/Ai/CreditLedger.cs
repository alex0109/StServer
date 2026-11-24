using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Ai;

namespace Application.Services.Ai;

// The one way a credit balance changes: a history row and the cached balance together, once per
// idempotency key. Callers hold the user's AI lock, so the balance can't be raced
public class CreditLedger
{
    private readonly ICreditRepository _repo;
    private readonly TimeProvider _time;

    public CreditLedger(ICreditRepository repo, TimeProvider time)
    {
        _repo = repo;
        _time = time;
    }

    public async Task<int> BalanceAsync(Guid userId) => (await _repo.GetWalletAsync(userId))?.Balance ?? 0;

    // False when this key was applied before: a repeated webhook or release changes nothing
    public async Task<bool> ChangeAsync(CreditTransaction change)
    {
        if (await _repo.HasTransactionAsync(change.IdempotencyKey)) return false;

        var wallet = await WalletAsync(change.UserId);
        wallet.Balance += change.Delta;
        wallet.UpdatedAt = change.CreatedAt;
        await _repo.AddTransactionAsync(change);
        return true;
    }

    public async Task<CreditWallet> WalletAsync(Guid userId)
    {
        var wallet = await _repo.GetWalletAsync(userId);
        if (wallet is not null) return wallet;

        var now = _time.GetUtcNow().UtcDateTime;
        wallet = new CreditWallet { UserId = userId, CreatedAt = now, UpdatedAt = now };
        await _repo.AddWalletAsync(wallet);
        return wallet;
    }

    public CreditTransaction Entry(Guid userId, int delta, CreditReason reason, string key) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Delta = delta,
        Reason = reason,
        IdempotencyKey = key,
        CreatedAt = _time.GetUtcNow().UtcDateTime,
    };
}
