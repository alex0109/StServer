using Application.Billing;
using Application.DTOs.Admin;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Subscription;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class AdminBillingStatsServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAdminBillingRepository> _repo = new();
    private readonly Mock<IBillingReports> _reports = new();
    private readonly List<UserSubscription> _subscriptions = [];
    private readonly List<Guid> _duoPayersWithMember = [];
    private readonly List<SubscriptionEventRow> _events = [];
    private readonly AdminBillingStatsService _sut;

    public AdminBillingStatsServiceTests()
    {
        _repo.Setup(x => x.ListSubscriptionsAsync()).ReturnsAsync(() => _subscriptions);
        _repo.Setup(x => x.ListDuoPayersWithMemberAsync()).ReturnsAsync(() => _duoPayersWithMember);
        _repo.Setup(x => x.ListSubscriptionEventsAsync(It.IsAny<DateTime>())).ReturnsAsync(() => _events);

        _reports.Setup(x => x.IsConfigured).Returns(true);
        _reports.Setup(x => x.GetPaymentsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _reports.Setup(x => x.GetBalanceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _reports.Setup(x => x.GetRenewalsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));

        _sut = new AdminBillingStatsService(_repo.Object, _reports.Object, time.Object, NullLogger<AdminBillingStatsService>.Instance);
    }

    private UserSubscription Add(
        SubscriptionTier tier,
        SubscriptionStatus status = SubscriptionStatus.Active,
        SubscriptionProvider provider = SubscriptionProvider.Stripe,
        DateTime? periodEnd = null,
        bool canceling = false)
    {
        var subscription = new UserSubscription
        {
            UserId = Guid.NewGuid(),
            Tier = tier,
            Status = status,
            Provider = provider,
            CurrentPeriodEnd = periodEnd ?? Now.AddDays(10),
            TrialEndsAt = status == SubscriptionStatus.Trialing ? Now.AddDays(5) : null,
            CancelAtPeriodEnd = canceling,
            CreatedAt = Now.AddMonths(-2),
        };
        _subscriptions.Add(subscription);
        return subscription;
    }

    private static DateTime Day(int month, int day, int year = 2026) => new(year, month, day, 9, 0, 0, DateTimeKind.Utc);

    private void Event(string subscription, string type, DateTime at, bool canceling = false) =>
        _events.Add(new SubscriptionEventRow(subscription, Guid.NewGuid(), type, at, canceling));

    [Fact]
    public async Task Counts_ThePlansInForce_ByWhoPays()
    {
        Add(SubscriptionTier.Plus);
        var duo = Add(SubscriptionTier.Duo);
        Add(SubscriptionTier.Plus, SubscriptionStatus.Trialing);
        Add(SubscriptionTier.Plus, SubscriptionStatus.PastDue, periodEnd: Now.AddDays(-2));
        Add(SubscriptionTier.Plus, canceling: true);
        Add(SubscriptionTier.Plus, provider: SubscriptionProvider.Manual, periodEnd: null);
        Add(SubscriptionTier.Plus, SubscriptionStatus.Canceled, periodEnd: Now.AddDays(-1));
        var lapsedDuo = Add(SubscriptionTier.Duo, SubscriptionStatus.Expired);
        _duoPayersWithMember.AddRange([duo.UserId, lapsedDuo.UserId]);

        var counts = (await _sut.GetAsync(TestContext.Current.CancellationToken)).Counts;

        counts.Plus.ShouldBe(3);
        counts.Duo.ShouldBe(1);
        counts.Trialing.ShouldBe(1);
        counts.PastDue.ShouldBe(1);
        counts.Canceling.ShouldBe(1);
        counts.Granted.ShouldBe(1);
        counts.DuoMembers.ShouldBe(1);
    }

    [Fact]
    public async Task Months_CountNewPlans_AndCancellationsWhenAskedFor()
    {
        // Asked to cancel in November, ends later
        Event("sub_a", BillingEventTypes.Created, Day(11, 5));
        Event("sub_a", "customer.subscription.updated", Day(11, 10), canceling: true);
        // Ended at once, without asking first
        Event("sub_b", BillingEventTypes.Created, Day(6, 3));
        Event("sub_b", BillingEventTypes.Deleted, Day(11, 15));
        // Asked, took it back, asked again; then ended: the end is no new cancellation
        Event("sub_c", BillingEventTypes.Created, Day(10, 2));
        Event("sub_c", "customer.subscription.updated", Day(10, 20), canceling: true);
        Event("sub_c", "customer.subscription.updated", Day(10, 21));
        Event("sub_c", "customer.subscription.updated", Day(11, 1), canceling: true);
        Event("sub_c", BillingEventTypes.Deleted, Day(11, 18));

        var stats = await _sut.GetAsync(TestContext.Current.CancellationToken);

        stats.Months.Count.ShouldBe(12);
        stats.Months[0].Month.ShouldBe("2025-12");
        stats.Months.Single(x => x.Month == "2026-11").ShouldBe(new AdminBillingMonthDto("2026-11", 1, 3, 0, 0));
        stats.Months.Single(x => x.Month == "2026-10").ShouldBe(new AdminBillingMonthDto("2026-10", 1, 1, 0, 0));
        stats.Months.Single(x => x.Month == "2026-06").New.ShouldBe(1);
        stats.Counts.NewLast30Days.ShouldBe(1);
        stats.Counts.CanceledLast30Days.ShouldBe(3);
    }

    [Fact]
    public async Task Revenue_ByPeriod_AndTheYearIfEveryPlanRenews()
    {
        _reports.Setup(x => x.GetPaymentsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new BillingPayment(Day(11, 3), 1000, "eur"),
            new BillingPayment(Day(10, 5), 2000, "eur"),
            new BillingPayment(Day(2, 10), 3000, "eur"),
            new BillingPayment(Day(12, 20, 2025), 500, "eur"),
            new BillingPayment(Day(11, 4), 1200, "usd"),
        ]);
        _reports.Setup(x => x.GetBalanceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new BillingBalanceEntry(Day(11, 3), 900, "eur"),
            new BillingBalanceEntry(Day(10, 5), 1800, "eur"),
            new BillingBalanceEntry(Day(2, 10), 2700, "eur"),
        ]);
        _reports.Setup(x => x.GetRenewalsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new BillingRenewal(null, SubscriptionTier.Plus, BillingInterval.Month, 1000, "eur", Day(12, 3)),
            new BillingRenewal(null, SubscriptionTier.Duo, BillingInterval.Year, 9600, "eur", Day(3, 1, 2027)),
        ]);

        var stats = await _sut.GetAsync(TestContext.Current.CancellationToken);

        stats.RevenueAvailable.ShouldBeTrue();
        stats.MainCurrency.ShouldBe("eur");
        var eur = stats.Revenue[0];
        eur.Gross.ShouldBe(new AdminMoneyPeriodsDto(1000, 3000, 6000));
        eur.Net.ShouldBe(new AdminMoneyPeriodsDto(900, 2700, 5400));
        eur.MonthlyRecurring.ShouldBe(1800);
        eur.ProjectedYear.ShouldBe(7000);

        var usd = stats.Revenue[1];
        usd.Currency.ShouldBe("usd");
        usd.Gross.Month.ShouldBe(1200);
        usd.Net.ShouldBeNull();

        stats.Months.Single(x => x.Month == "2026-11").Gross.ShouldBe(1000);
        stats.Months.Single(x => x.Month == "2026-11").Net.ShouldBe(900);
        stats.Months.Single(x => x.Month == "2025-12").Gross.ShouldBe(500);
    }

    [Fact]
    public async Task WhenStripeIsDown_ThePlansAreStillCounted()
    {
        Add(SubscriptionTier.Plus);
        _reports.Setup(x => x.GetPaymentsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BillingUnavailableException("Stripe answered 500"));

        var stats = await _sut.GetAsync(TestContext.Current.CancellationToken);

        stats.RevenueAvailable.ShouldBeFalse();
        stats.RevenueError.ShouldBe("Stripe answered 500");
        stats.Revenue.ShouldBeEmpty();
        stats.Counts.Plus.ShouldBe(1);
    }

    [Fact]
    public async Task WithoutStripe_TheReportsAreNotAsked()
    {
        _reports.Setup(x => x.IsConfigured).Returns(false);

        var stats = await _sut.GetAsync(TestContext.Current.CancellationToken);

        stats.RevenueAvailable.ShouldBeFalse();
        _reports.Verify(x => x.GetPaymentsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void RenewalsBefore_CountsEveryMonthlyPayment_AndOneYearly()
    {
        var end = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        AdminBillingStatsService.RenewalsBefore(new BillingRenewal(null, SubscriptionTier.Plus, BillingInterval.Month, 1, "eur", Day(11, 25)), end).ShouldBe(2);
        AdminBillingStatsService.RenewalsBefore(new BillingRenewal(null, SubscriptionTier.Plus, BillingInterval.Year, 1, "eur", Day(12, 30)), end).ShouldBe(1);
        AdminBillingStatsService.RenewalsBefore(new BillingRenewal(null, SubscriptionTier.Plus, BillingInterval.Month, 1, "eur", Day(1, 2, 2027)), end).ShouldBe(0);
    }
}
