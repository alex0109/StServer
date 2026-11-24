using Application.Admin;
using Application.Ai;
using Application.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services.Ai;
using Domain.Entities;
using Domain.Utility.Ai;
using Domain.Utility.Subscription;
using Moq;
using Shouldly;

namespace UnitTests.Ai;

public class AiUsageServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MonthStart = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeAiUsageRepository _usage = new();
    private readonly FakeCreditRepository _credits = new();
    private readonly Mock<ISubscriptionTierProvider> _plans = new();
    private readonly Mock<IAiCreditConsent> _consent = new();
    private readonly Mock<TimeProvider> _time = new();
    private readonly AiUsageService _sut;

    public AiUsageServiceTests()
    {
        OnPlan(SubscriptionTier.Free, MonthStart, MonthStart.AddMonths(1));
        _time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));

        _sut = new AiUsageService(
            _usage, _credits, new FakeLimitLock(), _plans.Object, TestPricing.Shipped(),
            new AiPricingOptions(), new AiOptions(), _time.Object, _consent.Object);
    }

    private void OnPlan(SubscriptionTier tier, DateTime start, DateTime end, Guid? userId = null) =>
        _plans.Setup(x => x.GetAiPlanAsync(userId ?? _userId)).ReturnsAsync(new AiPlanPeriod(tier, start, end));

    private void Consents() => _consent.Setup(x => x.UseCredits).Returns(true);

    // Earlier requests of this period, committed
    private void Used(AiFeature feature, int units, AiUsageSource source = AiUsageSource.Quota, DateTime? at = null, Guid? userId = null) =>
        _usage.Usages.Add(new AiUsage
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? _userId,
            Feature = feature,
            Units = units,
            Source = source,
            Status = AiUsageStatus.Succeeded,
            CreatedAt = at ?? Now.AddDays(-1),
        });

    private static AiCompletion Completion() => new() { Text = "{}", Provider = "Anthropic", Model = "claude-haiku-5-5" };

    [Fact]
    public async Task Quota_IsTakenFirst()
    {
        var reservation = await _sut.ReserveAsync(_userId, AiFeature.QuestionGeneration);

        reservation.Source.ShouldBe(AiUsageSource.Quota);
        _usage.Usages.ShouldHaveSingleItem().Status.ShouldBe(AiUsageStatus.Pending);
    }

    [Fact]
    public async Task OverQuota_WithoutConsent_IsDeniedWithThePriceInCredits()
    {
        Used(AiFeature.QuestionGeneration, 10);
        _credits.Give(_userId, 50);

        var denied = await Should.ThrowAsync<AiUsageDeniedException>(() => _sut.ReserveAsync(_userId, AiFeature.QuestionGeneration));

        denied.Code.ShouldBe(AiDenialCode.QuotaExhausted);
        denied.CreditsRequired.ShouldBe(1);
        denied.CreditBalance.ShouldBe(50);
        _credits.Balance(_userId).ShouldBe(50);
    }

    [Fact]
    public async Task OverQuota_WithConsent_CreditsPay_AndWithoutEnoughTheAnswerSaysSo()
    {
        Used(AiFeature.SummaryGeneration, 3);
        _credits.Give(_userId, 4);
        Consents();

        var reservation = await _sut.ReserveAsync(_userId, AiFeature.SummaryGeneration);

        reservation.Source.ShouldBe(AiUsageSource.Credits);
        reservation.Credits.ShouldBe(3);
        _credits.Balance(_userId).ShouldBe(1);
        _credits.Transactions.ShouldHaveSingleItem().Reason.ShouldBe(CreditReason.Spend);

        var denied = await Should.ThrowAsync<AiUsageDeniedException>(() => _sut.ReserveAsync(_userId, AiFeature.SummaryGeneration));
        denied.Code.ShouldBe(AiDenialCode.InsufficientCredits);
    }

    [Fact]
    public async Task AutoUse_PaysStudyAi_ButMediaAlwaysAsk()
    {
        _credits.Give(_userId, 500, autoUse: true);
        Used(AiFeature.WordExplanation, 100);
        Used(AiFeature.PodcastGeneration, 1, AiUsageSource.Trial);

        (await _sut.ReserveAsync(_userId, AiFeature.WordExplanation)).Source.ShouldBe(AiUsageSource.Credits);
        (await Should.ThrowAsync<AiUsageDeniedException>(() => _sut.ReserveAsync(_userId, AiFeature.PodcastGeneration)))
            .Code.ShouldBe(AiDenialCode.TrialUsed);
    }

    [Fact]
    public async Task PaidPlans_CountInTheirStripePeriod_FreeInTheCalendarMonth()
    {
        var periodStart = new DateTime(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);
        OnPlan(SubscriptionTier.Plus, periodStart, periodStart.AddMonths(1));
        // Before the Stripe period: the previous period's use
        Used(AiFeature.SummaryGeneration, 30, at: new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc));

        (await _sut.ReserveAsync(_userId, AiFeature.SummaryGeneration)).Source.ShouldBe(AiUsageSource.Quota);

        var usage = await _sut.GetUsageAsync(_userId);
        usage.PeriodStart.ShouldBe(periodStart);
        usage.ResetsAt.ShouldBe(periodStart.AddMonths(1));
        usage.Features.Single(x => x.Feature == AiFeature.SummaryGeneration).Used.ShouldBe(1);
    }

    [Fact]
    public async Task FreeMediaTrial_IsUsableOncePerLifetime()
    {
        var first = await _sut.ReserveAsync(_userId, AiFeature.PodcastGeneration);
        first.Source.ShouldBe(AiUsageSource.Trial);
        await _sut.CommitAsync(first.Id, Completion());

        var denied = await Should.ThrowAsync<AiUsageDeniedException>(() => _sut.ReserveAsync(_userId, AiFeature.PodcastGeneration));
        denied.Code.ShouldBe(AiDenialCode.TrialUsed);
        denied.CreditsRequired.ShouldBe(61);

        // Months later it is still used
        _time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now.AddMonths(5)));
        OnPlan(SubscriptionTier.Free, MonthStart.AddMonths(5), MonthStart.AddMonths(6));
        (await Should.ThrowAsync<AiUsageDeniedException>(() => _sut.ReserveAsync(_userId, AiFeature.PodcastGeneration)))
            .Code.ShouldBe(AiDenialCode.TrialUsed);
    }

    [Fact]
    public async Task Video_31Minutes_Take31QuotaMinutes_Or49Credits()
    {
        OnPlan(SubscriptionTier.Plus, MonthStart, MonthStart.AddMonths(1));

        (await _sut.ReserveAsync(_userId, AiFeature.VideoTranscript, 31)).Units.ShouldBe(31);

        // 120 a period: 31 + 70 used leaves 19, too few for another 31
        Used(AiFeature.VideoTranscript, 70);
        _credits.Give(_userId, 100);
        Consents();
        var paid = await _sut.ReserveAsync(_userId, AiFeature.VideoTranscript, 31);

        paid.Source.ShouldBe(AiUsageSource.Credits);
        paid.Credits.ShouldBe(49);
        _credits.Balance(_userId).ShouldBe(51);
    }

    [Fact]
    public async Task WordsFromText_ShareStartedBlocks_OfTenWords()
    {
        Used(AiFeature.WordLookup, 100);
        _credits.Give(_userId, 10);
        Consents();

        for (var i = 0; i < 10; i++) await _sut.ReserveAsync(_userId, AiFeature.WordLookup);
        _credits.Balance(_userId).ShouldBe(9);

        await _sut.ReserveAsync(_userId, AiFeature.WordLookup);
        _credits.Balance(_userId).ShouldBe(8);
    }

    [Fact]
    public async Task AFailedRequest_GivesItsCreditsBack_Once()
    {
        Used(AiFeature.MistakeExplanation, 5);
        _credits.Give(_userId, 10);
        Consents();
        var reservation = await _sut.ReserveAsync(_userId, AiFeature.MistakeExplanation);
        _credits.Balance(_userId).ShouldBe(8);

        await _sut.ReleaseAsync(reservation.Id);
        await _sut.ReleaseAsync(reservation.Id);

        _credits.Balance(_userId).ShouldBe(10);
        _usage.Usages.Single(x => x.Id == reservation.Id).Status.ShouldBe(AiUsageStatus.Failed);
    }

    [Fact]
    public async Task AShorterVideo_GivesTheRestBack()
    {
        Used(AiFeature.VideoTranscript, 30, AiUsageSource.Trial);
        _credits.Give(_userId, 100);
        Consents();
        var reservation = await _sut.ReserveAsync(_userId, AiFeature.VideoTranscript, 30);
        reservation.Credits.ShouldBe(42);

        await _sut.CommitAsync(reservation.Id, Completion(), units: 12, videoSeconds: 700);

        var usage = _usage.Usages.Single(x => x.Id == reservation.Id);
        usage.Units.ShouldBe(12);
        usage.Credits.ShouldBe(21);
        usage.VideoSeconds.ShouldBe(700);
        _credits.Balance(_userId).ShouldBe(79);
    }

    [Fact]
    public async Task StuckReservations_AreReleased_WithTheirCredits()
    {
        Used(AiFeature.EssayReview, 3);
        _credits.Give(_userId, 5);
        Consents();
        var stuck = await _sut.ReserveAsync(_userId, AiFeature.EssayReview);

        _time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now + AiUsageService.PendingTimeout + TimeSpan.FromMinutes(1)));
        (await _sut.ReleaseStaleAsync()).ShouldBe(1);

        _usage.Usages.Single(x => x.Id == stuck.Id).Status.ShouldBe(AiUsageStatus.Failed);
        _credits.Balance(_userId).ShouldBe(5);
    }

    [Fact]
    public async Task AnswerChecks_AreNeverSoldForCredits()
    {
        Used(AiFeature.AnswerGrading, 300);
        _credits.Give(_userId, 100, autoUse: true);
        Consents();

        var denied = await Should.ThrowAsync<AiUsageDeniedException>(() => _sut.ReserveAsync(_userId, AiFeature.AnswerGrading));

        denied.CreditsRequired.ShouldBeNull();
        _credits.Balance(_userId).ShouldBe(100);
    }

    [Fact]
    public async Task DuoSeats_HaveTheirOwnCountersAndWallets()
    {
        var partner = Guid.NewGuid();
        OnPlan(SubscriptionTier.Duo, MonthStart, MonthStart.AddMonths(1));
        OnPlan(SubscriptionTier.Duo, MonthStart, MonthStart.AddMonths(1), partner);
        Used(AiFeature.PodcastGeneration, 5);
        _credits.Give(partner, 100);
        Consents();

        (await Should.ThrowAsync<AiUsageDeniedException>(() => _sut.ReserveAsync(_userId, AiFeature.PodcastGeneration)))
            .Code.ShouldBe(AiDenialCode.InsufficientCredits);
        (await _sut.ReserveAsync(partner, AiFeature.PodcastGeneration)).Source.ShouldBe(AiUsageSource.Quota);
    }

    [Fact]
    public async Task Usage_ShowsUnitsTrialAndCredits()
    {
        Used(AiFeature.VideoTranscript, 12, AiUsageSource.Trial);
        _credits.Give(_userId, 7, autoUse: true);

        var usage = await _sut.GetUsageAsync(_userId);

        var video = usage.Features.Single(x => x.Feature == AiFeature.VideoTranscript);
        video.Limit.ShouldBe(0);
        video.TrialUsed.ShouldBe(12);
        video.TrialLimit.ShouldBe(30);
        video.Credits.ShouldBe(7);
        video.CreditBlock.ShouldBe(5);
        usage.CreditBalance.ShouldBe(7);
        usage.AutoUseCredits.ShouldBeTrue();
        usage.Features.Single(x => x.Feature == AiFeature.AnswerGrading).Credits.ShouldBeNull();
    }

    [Fact]
    public async Task ABackgroundExtra_NeverSpendsCredits_EvenWithAutoUse()
    {
        Used(AiFeature.WordLookup, 100);
        _credits.Give(_userId, 50, autoUse: true);
        Consents();

        await Should.ThrowAsync<AiUsageDeniedException>(() => _sut.ReserveAsync(_userId, AiFeature.WordLookup, allowCredits: false));

        _credits.Balance(_userId).ShouldBe(50);
    }

    // Empty answers were paid to the provider: three a day are forgiven, the fourth counts like a usable one
    [Fact]
    public async Task UnusableAnswers_AreForgivenThreeTimesADay_ThenCounted()
    {
        var statuses = new List<AiUsageStatus>();
        for (var i = 0; i < 4; i++)
        {
            var reservation = await _sut.ReserveAsync(_userId, AiFeature.QuestionGeneration);
            await _sut.SettleUnusableAsync(reservation.Id, Completion());
            statuses.Add(_usage.Usages.Single(x => x.Id == reservation.Id).Status);
        }

        statuses.ShouldBe([AiUsageStatus.Failed, AiUsageStatus.Failed, AiUsageStatus.Failed, AiUsageStatus.Succeeded]);
        (await _usage.SumUnitsAsync(_userId, AiFeature.QuestionGeneration, AiUsageSource.Quota, MonthStart)).ShouldBe(1);
    }

    // Paid with credits: forgiven answers give them back, a counted one keeps them
    [Fact]
    public async Task UnusableAnswers_PaidWithCredits_KeepThemOnceTheGraceIsUsed()
    {
        Used(AiFeature.MistakeExplanation, 5);
        _credits.Give(_userId, 100);
        Consents();
        ForgivenEarlier(AiFeature.MistakeExplanation, 3, Now.AddHours(-2));

        var reservation = await _sut.ReserveAsync(_userId, AiFeature.MistakeExplanation);
        await _sut.SettleUnusableAsync(reservation.Id, Completion());

        _credits.Balance(_userId).ShouldBe(98);
        _usage.Usages.Single(x => x.Id == reservation.Id).Status.ShouldBe(AiUsageStatus.Succeeded);
    }

    // The grace is per day: what was forgiven yesterday doesn't use up today's
    [Fact]
    public async Task UnusableAnswers_ForgivenYesterday_DontCountToday()
    {
        ForgivenEarlier(AiFeature.QuestionGeneration, 3, Now.AddHours(-25));

        var reservation = await _sut.ReserveAsync(_userId, AiFeature.QuestionGeneration);
        await _sut.SettleUnusableAsync(reservation.Id, Completion());

        _usage.Usages.Single(x => x.Id == reservation.Id).Status.ShouldBe(AiUsageStatus.Failed);
    }

    // A release with no answer from a provider (outage, lost process) is not a forgiven answer
    [Fact]
    public async Task ReleasesWithoutAnAnswer_DontUseTheGrace()
    {
        for (var i = 0; i < 5; i++)
        {
            var failed = await _sut.ReserveAsync(_userId, AiFeature.QuestionGeneration);
            await _sut.ReleaseAsync(failed.Id);
        }

        var reservation = await _sut.ReserveAsync(_userId, AiFeature.QuestionGeneration);
        await _sut.SettleUnusableAsync(reservation.Id, Completion());

        _usage.Usages.Single(x => x.Id == reservation.Id).Status.ShouldBe(AiUsageStatus.Failed);
    }

    private void ForgivenEarlier(AiFeature feature, int count, DateTime at)
    {
        for (var i = 0; i < count; i++)
        {
            _usage.Usages.Add(new AiUsage
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Feature = feature,
                Status = AiUsageStatus.Failed,
                Provider = "Anthropic",
                CreatedAt = at,
            });
        }
    }
}
