using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Ai;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class CreditRepository : ICreditRepository
{
    private readonly AppDbContext _db;

    public CreditRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<CreditWallet?> GetWalletAsync(Guid userId) =>
        _db.CreditWallets.FirstOrDefaultAsync(x => x.UserId == userId);

    public async Task AddWalletAsync(CreditWallet wallet) => await _db.CreditWallets.AddAsync(wallet);

    public Task<bool> HasTransactionAsync(string idempotencyKey) =>
        _db.CreditTransactions.AnyAsync(x => x.IdempotencyKey == idempotencyKey);

    public async Task AddTransactionAsync(CreditTransaction transaction) => await _db.CreditTransactions.AddAsync(transaction);

    public Task<CreditTransaction?> FindPurchaseAsync(string stripeReference) =>
        _db.CreditTransactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StripeReference == stripeReference && x.Reason == CreditReason.Purchase);

    public async Task<int> SumRefundedAsync(string stripeReference) =>
        -await _db.CreditTransactions
            .Where(x => x.StripeReference == stripeReference && x.Reason == CreditReason.Refund)
            .SumAsync(x => x.Delta);

    public async Task<(List<CreditTransaction> Items, int Total)> ListAsync(Guid userId, int skip, int take)
    {
        var query = _db.CreditTransactions.AsNoTracking().Where(x => x.UserId == userId);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(skip).Take(take).ToListAsync();
        return (items, total);
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
