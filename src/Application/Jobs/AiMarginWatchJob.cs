using Application.Admin;
using Application.Ai.Pricing;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Jobs;

// Hourly look at real costs: warns when the fallback answers more than the cost model assumes, or a
// user's AI this month costs more than their plan brings in
public class AiMarginWatchJob
{
    private const int UsersChecked = 100;

    private readonly IAdminStatsRepository _repo;
    private readonly ISubscriptionTierProvider _tiers;
    private readonly PricingOptions _pricing;
    private readonly AiPricingOptions _prices;
    private readonly TimeProvider _time;
    private readonly ILogger<AiMarginWatchJob> _logger;

    public AiMarginWatchJob(
        IAdminStatsRepository repo,
        ISubscriptionTierProvider tiers,
        PricingOptions pricing,
        AiPricingOptions prices,
        TimeProvider time,
        ILogger<AiMarginWatchJob> logger)
    {
        _repo = repo;
        _tiers = tiers;
        _pricing = pricing;
        _prices = prices;
        _time = time;
        _logger = logger;
    }

    public async Task Check()
    {
        var now = _time.GetUtcNow().UtcDateTime;

        var day = await _repo.GetAiCostsAsync(now.AddHours(-24));
        var withFallback = day.Where(x => _pricing.FeatureOf(x.Feature)?.FallbackUnitCost is not null).ToList();
        var total = withFallback.Sum(x => x.Requests);
        var share = total == 0 ? 0 : (decimal)withFallback.Where(x => x.IsFallback).Sum(x => x.Requests) / total;
        if (share > _pricing.FallbackShare)
        {
            _logger.LogWarning("AI fallback answered {Share:P1} of {Total} requests in 24 hours, the cost model assumes {Assumed:P0}",
                share, total, _pricing.FallbackShare);
        }

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var users = await _repo.GetUserCostsAsync(monthStart, _prices.SpeechPerMillionCharacters ?? 0, UsersChecked);
        var over = 0;
        foreach (var user in users)
        {
            var tier = await _tiers.GetTierAsync(user.UserId);
            if (user.Cost > _pricing.MonthlyNetPerSeat(tier)) over++;
        }

        if (over > 0)
        {
            _logger.LogWarning("{Count} users cost more in AI this month than their plan brings in; see the admin panel's AI margins", over);
        }
    }
}
