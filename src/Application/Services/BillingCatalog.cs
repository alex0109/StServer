using Application.Ai;
using Application.Billing;
using Application.DTOs.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Application.Limits;
using Domain.Utility.Ai;
using Domain.Utility.Subscription;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class BillingCatalog : IBillingCatalog
{
    private readonly IEnumerable<IBillingProvider> _providers;
    private readonly ContentLimitsOptions _limits;
    private readonly AiOptions _ai;
    private readonly ILogger<BillingCatalog> _logger;

    public BillingCatalog(
        IEnumerable<IBillingProvider> providers,
        ContentLimitsOptions limits,
        AiOptions ai,
        ILogger<BillingCatalog> logger)
    {
        _providers = providers;
        _limits = limits;
        _ai = ai;
        _logger = logger;
    }

    public async Task<BillingPlansDto> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var prices = await PricesAsync(cancellationToken);

        var plans = new[] { SubscriptionTier.Free, SubscriptionTier.Plus, SubscriptionTier.Pro }
            .Select(tier => new BillingPlanDto(
                tier,
                prices
                    // BillingPlan values are the tiers' values
                    .Where(x => (SubscriptionTier)x.Plan == tier)
                    .OrderBy(x => x.Interval)
                    .Select(x => new BillingPriceDto(x.Interval, x.Amount, x.Currency))
                    .ToList(),
                _limits.For(tier),
                Enum.GetValues<AiFeature>().ToDictionary(feature => feature, feature => _ai.GetMonthlyQuota(tier, feature))))
            .ToList();

        return new BillingPlansDto(prices.Count > 0, plans);
    }

    // Without prices nothing can be bought, but the page still shows what each plan gives
    private async Task<IReadOnlyList<BillingPrice>> PricesAsync(CancellationToken cancellationToken)
    {
        var billing = _providers.FirstOrDefault(x => x.IsConfigured);
        if (billing is null) return [];

        try
        {
            return await billing.GetPricesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or BillingUnavailableException)
        {
            _logger.LogWarning("Prices from {Provider} are unavailable: {Reason}", billing.Name, exception.Message);
            return [];
        }
    }
}
