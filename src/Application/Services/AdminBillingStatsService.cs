using Application.Billing;
using Application.DTOs.Admin;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Utility.Subscription;
using Microsoft.Extensions.Logging;

namespace Application.Services;

// Subscriptions and money for the admin panel. Who has which plan comes from the database, the money
// from the billing provider. Months and quarters are calendar ones in UTC
public class AdminBillingStatsService : IAdminBillingStatsService
{
    private const int MonthsShown = 12;
    private const int RecentDays = 30;

    private readonly IAdminBillingRepository _repo;
    private readonly IBillingReports _reports;
    private readonly TimeProvider _time;
    private readonly ILogger<AdminBillingStatsService> _logger;

    public AdminBillingStatsService(
        IAdminBillingRepository repo,
        IBillingReports reports,
        TimeProvider time,
        ILogger<AdminBillingStatsService> logger)
    {
        _repo = repo;
        _reports = reports;
        _time = time;
        _logger = logger;
    }

    public async Task<AdminBillingStatsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var periods = new Periods(now);
        var since = periods.Series < periods.Year ? periods.Series : periods.Year;

        var counts = await CountAsync(now);
        var flows = FlowsByMonth(await _repo.ListSubscriptionEventsAsync(since));

        string? error = null;
        IReadOnlyList<BillingPayment> payments = [];
        IReadOnlyList<BillingBalanceEntry> balance = [];
        IReadOnlyList<BillingRenewal> renewals = [];

        if (!_reports.IsConfigured)
        {
            error = "Stripe is not set up";
        }
        else
        {
            try
            {
                payments = await _reports.GetPaymentsAsync(since, cancellationToken);
                balance = await _reports.GetBalanceAsync(since, cancellationToken);
                renewals = await _reports.GetRenewalsAsync(cancellationToken);
            }
            catch (BillingUnavailableException exception)
            {
                // The plans are still worth seeing while the provider is down
                _logger.LogWarning("Billing reports are unavailable: {Reason}", exception.Message);
                error = exception.Message;
                (payments, balance, renewals) = ([], [], []);
            }
        }

        var revenue = Revenue(payments, balance, renewals, periods);
        var main = revenue.FirstOrDefault()?.Currency;

        var months = Enumerable.Range(0, MonthsShown)
            .Select(i => periods.Series.AddMonths(i))
            .Select(month =>
            {
                var key = MonthKey(month);
                var flow = flows.GetValueOrDefault(key);
                return new AdminBillingMonthDto(
                    key,
                    flow?.New ?? 0,
                    flow?.Canceled ?? 0,
                    payments.Where(x => x.Currency == main && MonthKey(x.PaidAt) == key).Sum(x => x.Amount),
                    balance.Where(x => x.Currency == main && MonthKey(x.At) == key).Sum(x => x.Net));
            })
            .ToList();

        var recent = flows.Values.SelectMany(x => x.Dates).Where(x => x.At >= now.AddDays(-RecentDays)).ToList();

        return new AdminBillingStatsDto(
            error is null,
            error,
            main,
            counts with
            {
                NewLast30Days = recent.Count(x => !x.IsCancel),
                CanceledLast30Days = recent.Count(x => x.IsCancel),
            },
            revenue,
            months,
            now);
    }

    private async Task<AdminBillingCountsDto> CountAsync(DateTime now)
    {
        var subscriptions = await _repo.ListSubscriptionsAsync();
        var inForce = subscriptions
            .Select(x => (Subscription: x, Tier: SubscriptionRules.EffectiveTier(x, now)))
            .Where(x => x.Tier != SubscriptionTier.Free)
            .ToList();

        var paid = inForce.Where(x => x.Subscription.Provider != SubscriptionProvider.Manual).ToList();
        var pastTrial = paid.Where(x => x.Subscription.Status != SubscriptionStatus.Trialing).ToList();

        var duoPayers = inForce.Where(x => x.Tier == SubscriptionTier.Duo).Select(x => x.Subscription.UserId).ToHashSet();
        var duoMembers = (await _repo.ListDuoPayersWithMemberAsync()).Count(duoPayers.Contains);

        return new AdminBillingCountsDto(
            Plus: pastTrial.Count(x => x.Tier == SubscriptionTier.Plus),
            Duo: pastTrial.Count(x => x.Tier == SubscriptionTier.Duo),
            Trialing: paid.Count(x => x.Subscription.Status == SubscriptionStatus.Trialing),
            PastDue: paid.Count(x => x.Subscription.Status == SubscriptionStatus.PastDue),
            // A cancelled plan runs to the end of what was paid for
            Canceling: paid.Count(x => x.Subscription.CancelAtPeriodEnd || x.Subscription.Status == SubscriptionStatus.Canceled),
            Granted: inForce.Count(x => x.Subscription.Provider == SubscriptionProvider.Manual),
            DuoMembers: duoMembers,
            NewLast30Days: 0,
            CanceledLast30Days: 0);
    }

    private sealed record Flow(DateTime At, bool IsCancel);

    private sealed class MonthFlow
    {
        public int New { get; set; }
        public int Canceled { get; set; }
        public List<Flow> Dates { get; } = [];
    }

    // Walks each subscription's webhooks in order. A cancellation is the moment it was asked for;
    // a plan that ended without one (cancelled at once, or never paid) counts when it ends
    private static Dictionary<string, MonthFlow> FlowsByMonth(List<SubscriptionEventRow> events)
    {
        var months = new Dictionary<string, MonthFlow>();

        void Add(DateTime at, bool isCancel)
        {
            var key = MonthKey(at);
            if (!months.TryGetValue(key, out var month)) months[key] = month = new MonthFlow();
            if (isCancel) month.Canceled++;
            else month.New++;
            month.Dates.Add(new Flow(at, isCancel));
        }

        var bySubscription = events
            .Where(x => x.SubscriptionId is not null || x.UserId is not null)
            .GroupBy(x => x.SubscriptionId ?? x.UserId!.Value.ToString());

        foreach (var subscription in bySubscription)
        {
            var canceling = false;
            foreach (var item in subscription.OrderBy(x => x.OccurredAt))
            {
                switch (item.Type)
                {
                    case BillingEventTypes.Created:
                        Add(item.OccurredAt, isCancel: false);
                        canceling = item.CancelAtPeriodEnd;
                        break;
                    case BillingEventTypes.Deleted:
                        if (!canceling) Add(item.OccurredAt, isCancel: true);
                        canceling = false;
                        break;
                    default:
                        // Asked to cancel; taking it back and asking again counts again
                        if (item.CancelAtPeriodEnd && !canceling) Add(item.OccurredAt, isCancel: true);
                        canceling = item.CancelAtPeriodEnd;
                        break;
                }
            }
        }

        return months;
    }

    private static List<AdminRevenueDto> Revenue(
        IReadOnlyList<BillingPayment> payments,
        IReadOnlyList<BillingBalanceEntry> balance,
        IReadOnlyList<BillingRenewal> renewals,
        Periods periods)
    {
        var currencies = payments.Select(x => x.Currency)
            .Concat(balance.Select(x => x.Currency))
            .Concat(renewals.Select(x => x.Currency))
            .Distinct();

        AdminMoneyPeriodsDto Sum<T>(IEnumerable<T> rows, Func<T, DateTime> at, Func<T, long> amount)
        {
            var list = rows.ToList();
            return new AdminMoneyPeriodsDto(
                list.Where(x => at(x) >= periods.Month).Sum(amount),
                list.Where(x => at(x) >= periods.Quarter).Sum(amount),
                list.Where(x => at(x) >= periods.Year).Sum(amount));
        }

        return currencies
            .Select(currency =>
            {
                var paid = payments.Where(x => x.Currency == currency).ToList();
                var kept = balance.Where(x => x.Currency == currency).ToList();
                var renewing = renewals.Where(x => x.Currency == currency).ToList();

                var gross = Sum(paid, x => x.PaidAt, x => x.Amount);
                var net = kept.Count == 0 ? null : Sum(kept, x => x.At, x => x.Net);
                var monthly = (long)Math.Round(renewing.Sum(x => x.Interval == BillingInterval.Year ? x.Amount / 12m : x.Amount));
                var ahead = renewing.Sum(x => x.Amount * RenewalsBefore(x, periods.NextYear));

                return new AdminRevenueDto(currency, gross, net, monthly, gross.Year + ahead);
            })
            // The account's own currency first: most money ends up there
            .OrderByDescending(x => x.Net?.Year ?? -1)
            .ThenByDescending(x => x.Gross.Year)
            .ThenByDescending(x => x.MonthlyRecurring)
            .ToList();
    }

    // How many times a plan is paid again before the given moment
    public static int RenewalsBefore(BillingRenewal renewal, DateTime end)
    {
        if (renewal.RenewsAt >= end) return 0;
        if (renewal.Interval == BillingInterval.Year) return 1;

        var count = 0;
        for (var next = renewal.RenewsAt; next < end; next = next.AddMonths(1)) count++;
        return count;
    }

    private static string MonthKey(DateTime at) => $"{at.Year:D4}-{at.Month:D2}";

    private sealed record Periods(DateTime Now)
    {
        public DateTime Month { get; } = new(Now.Year, Now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTime Quarter { get; } = new(Now.Year, (Now.Month - 1) / 3 * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTime Year { get; } = new(Now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTime NextYear { get; } = new(Now.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // The first of the months on the chart
        public DateTime Series { get; } = new DateTime(Now.Year, Now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(MonthsShown - 1));
    }
}
