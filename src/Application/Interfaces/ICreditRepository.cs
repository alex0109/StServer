using Domain.Entities;

namespace Application.Interfaces;

// Credit wallets and their append-only history. Changes run under the user's AI lock
public interface ICreditRepository
{
    // Tracked, so a change is saved with the rest
    Task<CreditWallet?> GetWalletAsync(Guid userId);
    Task AddWalletAsync(CreditWallet wallet);

    Task<bool> HasTransactionAsync(string idempotencyKey);
    Task AddTransactionAsync(CreditTransaction transaction);

    // The purchase a Stripe payment made, and how much of it refunds took back already
    Task<CreditTransaction?> FindPurchaseAsync(string stripeReference);
    Task<int> SumRefundedAsync(string stripeReference);

    Task<(List<CreditTransaction> Items, int Total)> ListAsync(Guid userId, int skip, int take);

    Task SaveChangesAsync();
}
