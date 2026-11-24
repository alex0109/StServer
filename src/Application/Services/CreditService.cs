using Application.Ai.Pricing;
using Application.Billing;
using Application.Constants;
using Application.DTOs.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services.Ai;
using Domain.Utility.Ai;
using Microsoft.Extensions.Logging;

namespace Application.Services;

// Prepaid AI credits: the wallet, its history, buying packs and applying the provider's payments
// and refunds. Every balance change goes through the ledger under the user's AI lock
public class CreditService : ICreditService
{
    public const int MaxPageSize = 50;

    private readonly ICreditRepository _repo;
    private readonly IBillingRepository _billing;
    private readonly IEnumerable<IBillingProvider> _providers;
    private readonly ILimitLock _limitLock;
    private readonly PricingOptions _pricing;
    private readonly IUserContext _user;
    private readonly ILogger<CreditService> _logger;
    private readonly CreditLedger _ledger;

    public CreditService(
        ICreditRepository repo,
        IBillingRepository billing,
        IEnumerable<IBillingProvider> providers,
        ILimitLock limitLock,
        PricingOptions pricing,
        IUserContext user,
        TimeProvider time,
        ILogger<CreditService> logger)
    {
        _repo = repo;
        _billing = billing;
        _providers = providers;
        _limitLock = limitLock;
        _pricing = pricing;
        _user = user;
        _logger = logger;
        _ledger = new CreditLedger(repo, time);
    }

    public CreditPricingDto GetPricing() => new(
        _providers.Any(x => x.IsConfigured),
        _pricing.Packs.Select(x => new CreditPackDto(x.Id, x.Credits, x.Price, x.Currency)).ToList(),
        _pricing.Features
            .Where(x => x.Value.Credits is not null)
            .Select(x => new CreditPriceDto(x.Key, AiBuckets.Of(x.Key), x.Value.Credits!.Value, Math.Max(1, x.Value.CreditBlock)))
            .OrderBy(x => x.Feature)
            .ToList());

    public async Task<CreditWalletDto> GetWalletAsync()
    {
        var wallet = await _repo.GetWalletAsync(_user.UserId);
        return new CreditWalletDto(wallet?.Balance ?? 0, wallet?.AutoUseForStudy ?? false);
    }

    public async Task<CreditHistoryDto> GetHistoryAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, total) = await _repo.ListAsync(_user.UserId, (page - 1) * pageSize, pageSize);
        return new CreditHistoryDto(
            items.Select(x => new CreditTransactionDto(x.Id, x.Delta, x.Reason, x.Feature, x.PackId, x.Note, x.CreatedAt)).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<CreditWalletDto> SetAutoUseAsync(bool enabled)
    {
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.AiUsage, _user.UserId);

        var wallet = await _ledger.WalletAsync(_user.UserId);
        wallet.AutoUseForStudy = enabled;
        await _repo.SaveChangesAsync();
        await limitLock.CommitAsync();

        return new CreditWalletDto(wallet.Balance, wallet.AutoUseForStudy);
    }

    public async Task<string> CreateCheckoutAsync(string? packId, string? language)
    {
        var pack = _pricing.PackOf(packId) ?? throw new BadRequestException("Unknown credit pack");
        var billing = _providers.FirstOrDefault(x => x.IsConfigured)
                      ?? throw new BillingUnavailableException("Payments are not set up yet");

        // The customer of an earlier plan, so one person stays one customer in Stripe
        var subscription = await _billing.GetSubscriptionAsync(_user.UserId);
        var customerId = subscription?.Provider == billing.Provider ? subscription.ProviderCustomerId : null;

        return await billing.CreateCreditCheckoutUrlAsync(new CreditCheckout(
            _user.UserId, _user.Email, pack.Id, pack.Credits, pack.Price, pack.Currency, customerId, language));
    }

    public async Task<string?> ApplyAsync(CreditEvent credit)
    {
        return credit.Kind == CreditEventKind.Purchase ? await PurchaseAsync(credit) : await RefundAsync(credit);
    }

    private async Task<string?> PurchaseAsync(CreditEvent credit)
    {
        if (credit.UserId is not { } userId) return "No user for this purchase";
        // Credits come from the server's packs, never from what the client or the event says
        if (_pricing.PackOf(credit.PackId) is not { } pack) return $"Unknown credit pack {credit.PackId}";

        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.AiUsage, userId);

        // One grant per checkout, however many times its webhook comes
        var entry = _ledger.Entry(userId, pack.Credits, CreditReason.Purchase, $"purchase:{credit.SessionId ?? credit.EventId}");
        entry.PackId = pack.Id;
        entry.StripeReference = credit.Reference ?? credit.SessionId;
        var applied = await _ledger.ChangeAsync(entry);

        await _repo.SaveChangesAsync();
        await limitLock.CommitAsync();

        return applied ? null : "Granted already";
    }

    // A refund takes back the refunded share of the pack, down to zero at most; what was spent
    // already is left for a person to look at
    private async Task<string?> RefundAsync(CreditEvent credit)
    {
        if (credit.Reference is null) return "No payment in this refund";

        // Most refunds are of subscription invoices, not credit packs
        var purchase = await _repo.FindPurchaseAsync(credit.Reference);
        if (purchase is null) return "Not a credit purchase";

        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.AiUsage, purchase.UserId);

        var share = credit.Amount > 0 ? Math.Min(1m, (decimal)credit.AmountRefunded / credit.Amount) : 1m;
        var target = (int)Math.Floor(purchase.Delta * share);
        var toTake = target - await _repo.SumRefundedAsync(credit.Reference);
        if (toTake <= 0)
        {
            await limitLock.CommitAsync();
            return "Nothing more to take back";
        }

        var balance = await _ledger.BalanceAsync(purchase.UserId);
        var taken = Math.Min(Math.Max(0, balance), toTake);

        var entry = _ledger.Entry(purchase.UserId, -taken, CreditReason.Refund, $"refund:{credit.EventId}");
        entry.PackId = purchase.PackId;
        entry.StripeReference = credit.Reference;
        if (taken < toTake)
        {
            entry.Note = $"{toTake - taken} refunded credits were spent already: review by hand";
            _logger.LogWarning("Credit refund of {Reference}: {Missing} of {Credits} credits were spent already, review by hand",
                credit.Reference, toTake - taken, toTake);
        }

        await _ledger.ChangeAsync(entry);
        await _repo.SaveChangesAsync();
        await limitLock.CommitAsync();

        return null;
    }
}
