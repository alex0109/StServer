using Application.Billing;

namespace Application.Interfaces;

// Promo codes kept by the billing provider: buyers type them on its checkout page,
// and it counts and limits their use
public interface IBillingPromotions
{
    bool IsConfigured { get; }

    // The discount itself; returns its id
    Task<string> CreateDiscountAsync(PromoBatch batch, CancellationToken cancellationToken = default);

    // Codes for a discount; the ones the provider refused (e.g. taken already) are not in the answer
    Task<IReadOnlyList<string>> AddCodesAsync(
        string discountId, IReadOnlyList<string> codes, PromoCodeLimits limits, CancellationToken cancellationToken = default);

    // Batches made from the admin panel, newest first
    Task<IReadOnlyList<PromoBatchInfo>> ListAsync(CancellationToken cancellationToken = default);

    // Switches off every code of a discount; returns how many were still on
    Task<int> DeactivateAsync(string discountId, CancellationToken cancellationToken = default);
}
