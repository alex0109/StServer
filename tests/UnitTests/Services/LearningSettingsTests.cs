using Application.DTOs.Flashcard;
using Application.DTOs.Notification;
using Application.DTOs.Statistics;
using Application.Email;
using Application.Evaluators;
using Application.Exceptions;
using Application.Interfaces;
using Application.Notifications;
using Application.Profile;
using Application.Review;
using Application.Schedulers;
using Application.Schedulers.Fsrs;
using Application.Services;
using Application.Statistics;
using Domain.Entities;
using Domain.Utility.Flashcard;
using Domain.Utility.Notification;
using Domain.Utility.Review;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class LearningRetentionTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private static readonly FsrsFlashcardScheduler Fsrs = new(new FsrsOptions { EnableFuzzing = false }, new Random(1));

    // A new word through its learning steps, at the same moments for every retention
    private static FlashcardCard Learned(IFlashcardScheduler scheduler)
    {
        var card = scheduler.Schedule(FlashcardCard.CreateNew(Start), ReviewRating.Good, Start);
        return scheduler.Schedule(card, ReviewRating.Good, Start.AddMinutes(10));
    }

    [Fact]
    public void WithoutSettings_TheIntervalsAreExactlyTheOldOnes()
    {
        UserFlashcardScheduler.For(Fsrs, null).ShouldBeSameAs(Fsrs);
        UserFlashcardScheduler.For(Fsrs, new SchedulerOverrides(null)).ShouldBeSameAs(Fsrs);

        var card = FlashcardCard.CreateNew(Start);
        Fsrs.Schedule(card, ReviewRating.Easy, Start).ShouldBe(Fsrs.Schedule(card, ReviewRating.Easy, Start, null));
    }

    [Fact]
    public void HigherRetention_BringsWordsBackSooner()
    {
        var standard = Learned(Fsrs);
        var careful = Learned(UserFlashcardScheduler.For(Fsrs, new SchedulerOverrides(0.97)));
        var relaxed = Learned(UserFlashcardScheduler.For(Fsrs, new SchedulerOverrides(0.80)));

        standard.State.ShouldBe(FlashcardState.Review);
        careful.ScheduledDays.ShouldBeLessThan(standard.ScheduledDays);
        relaxed.ScheduledDays.ShouldBeGreaterThan(standard.ScheduledDays);
        // Memory itself is the same, only the next date moves
        careful.Stability.ShouldBe(standard.Stability);
    }
}

public class DailyLimitTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc);

    private readonly Guid _userId = Guid.NewGuid();
    private readonly UserProfileServiceTests.FakeProfiles _profiles = new();
    private readonly Mock<IStatisticsRepository> _statistics = new();

    private LearningSettingsProvider Provider() => new(_profiles, _statistics.Object, new NotificationOptions());

    private void Profile(int? newPerDay = null, int? maxReviews = null) =>
        _profiles.Rows.Add(new UserProfile
        {
            UserId = _userId,
            InterfaceLanguage = "uk",
            NewCardsPerDay = newPerDay,
            MaxReviewsPerDay = maxReviews,
            CreatedAt = Now,
        });

    [Fact]
    public async Task NewWordsLimit_CountsWhatWasAlreadyLearnedToday()
    {
        Profile(newPerDay: 10, maxReviews: 100);
        _statistics.Setup(x => x.CountReviewsAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync((97, 8));

        var budget = await Provider().GetBudgetAsync(_userId, Now.Date, Now.Date.AddDays(1));

        budget.NewWordsLeft.ShouldBe(2);
        budget.ReviewsLeft.ShouldBe(3);
    }

    [Fact]
    public async Task NoSettings_NoLimits()
    {
        _statistics.Setup(x => x.CountReviewsAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync((500, 200));

        var budget = await Provider().GetBudgetAsync(_userId, Now.Date, Now.Date.AddDays(1));

        budget.NewWordsLeft.ShouldBeNull();
        budget.ReviewsLeft.ShouldBeNull();
    }

    private ReviewService Today(DailyBudget budget, Mock<IFlashcardService> flashcards)
    {
        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(_userId);
        var zones = new Mock<IUserTimeZoneProvider>();
        zones.Setup(x => x.GetAsync(_userId)).ReturnsAsync(TimeZoneInfo.Utc);
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));
        var learning = new Mock<ILearningSettingsProvider>();
        learning.Setup(x => x.GetBudgetAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).ReturnsAsync(budget);
        learning.Setup(x => x.GetAsync(_userId)).ReturnsAsync(new LearningSettings(null, 3, 50, 9, 20, null, 2));

        var repo = new Mock<IReviewRepository>();
        repo.Setup(x => x.GetDueQuestionsAsync(_userId, It.IsAny<DateTime>())).ReturnsAsync([]);

        return new ReviewService(
            repo.Object, _statistics.Object, flashcards.Object,
            new Mock<IQuestionReviewService>().Object, new Mock<IAnswerEvaluationService>().Object,
            new ConfidenceReviewRatingResolver(), zones.Object, user.Object, new ReviewOptions(), time.Object,
            learning.Object);
    }

    [Fact]
    public async Task TodayQueue_GivesNoMoreNewWordsThanTheDayAllows()
    {
        var flashcards = new Mock<IFlashcardService>();

        await Today(new DailyBudget(10, 1, NewWordsLeft: 2, ReviewsLeft: 40), flashcards).StartWordsAsync(new FlashcardSessionCreateDto());

        flashcards.Verify(x => x.StartTodaySessionAsync(It.IsAny<FlashcardSessionCreateDto>(), It.IsAny<DateTime>(), 2, 40));
    }

    [Fact]
    public async Task TodayQueue_StopsAtTheDailyMaximum()
    {
        var flashcards = new Mock<IFlashcardService>();

        await Should.ThrowAsync<ConflictException>(() =>
            Today(new DailyBudget(50, 3, 0, ReviewsLeft: 0), flashcards).StartWordsAsync(new FlashcardSessionCreateDto()));

        flashcards.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TodaySummary_ShowsWhatIsLeft()
    {
        _statistics.Setup(x => x.GetDueWordsAsync(_userId, It.IsAny<DateTime>())).ReturnsAsync(
            [new DictionaryDueDto { DictionaryId = Guid.NewGuid(), MaterialId = Guid.NewGuid(), Title = "A", Due = 30, New = 40 }]);

        var today = await Today(new DailyBudget(45, 2, 1, 5), new Mock<IFlashcardService>()).GetTodayAsync();

        today.WordsInSession.ShouldBe(5);
        today.ReviewsDone.ShouldBe(45);
        today.NewWordsLeft.ShouldBe(1);
    }
}

public class ReminderHourTests
{
    private readonly Mock<INotificationRepository> _repo = new();
    private readonly Mock<IStudyActivityService> _activity = new();

    public ReminderHourTests()
    {
        _repo.Setup(x => x.TryAddAsync(It.IsAny<UserNotification>())).ReturnsAsync(true);
        _activity.Setup(x => x.GetTodayAsync(It.IsAny<Guid>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new TodayDto
            {
                LocalDate = "2026-10-30", DueWords = 5, NewWords = 0, Dictionaries = [],
                StreakDays = 0, StudiedToday = false, StreakAtRisk = false,
            });
        _activity.Setup(x => x.GetStreakAsync(It.IsAny<Guid>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new StudyStreakDto());
    }

    private NotificationGenerator Generator() => new(
        _repo.Object, new Mock<IUserProfileRepository>().Object, _activity.Object, new Mock<IEmailSender>().Object,
        new NotificationOptions(), new EmailOptions(), TimeProvider.System, NullLogger<NotificationGenerator>.Instance);

    private static NotificationRecipient Recipient(string timeZone, int? dailyHour) =>
        new(Guid.NewGuid(), timeZone, "en-GB", null, false, false, false, DailyReminderHour: dailyHour);

    [Theory]
    // 6:30 in Oslo (UTC+1 in winter) and in New York (UTC-4 until 1 November)
    [InlineData("Europe/Oslo", 5)]
    [InlineData("America/New_York", 10)]
    public async Task OwnHour_IsTheUsersLocalHour(string timeZone, int utcHour)
    {
        var now = new DateTime(2026, 10, 30, utcHour, 30, 0, DateTimeKind.Utc);

        (await Generator().GenerateForUserAsync(Recipient(timeZone, dailyHour: 6), now)).ShouldBe(1);
        // The global hour (9) is hours away for someone without their own
        (await Generator().GenerateForUserAsync(Recipient(timeZone, dailyHour: null), now)).ShouldBe(0);
    }

    [Fact]
    public async Task AChangedHour_DoesNotRemindTwiceADay()
    {
        var recipient = Recipient("Europe/Oslo", dailyHour: 6);
        _repo.Setup(x => x.ExistsAsync(recipient.UserId, NotificationType.DailyReview, "2026-10-30")).ReturnsAsync(true);

        (await Generator().GenerateForUserAsync(recipient, new DateTime(2026, 10, 30, 5, 30, 0, DateTimeKind.Utc)))
            .ShouldBe(0);
    }
}

public class StreakFreezeTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    private static HashSet<DateOnly> Days(params int[] daysAgo) => daysAgo.Select(x => Today.AddDays(-x)).ToHashSet();

    [Fact]
    public void AMissedDay_TakesOneFreeze_AndTheStreakGoesOn()
    {
        var streak = StudyCalendar.Streak(Days(0, 1, 3, 4), Today, freezesPerMonth: 2);

        streak.Current.ShouldBe(4);
        streak.Frozen.ShouldBe([Today.AddDays(-2)]);
        streak.FreezesLeftThisMonth.ShouldBe(1);
    }

    [Fact]
    public void TheSameMissedDay_IsPaidForOnce()
    {
        var days = Days(0, 1, 3, 4);

        var first = StudyCalendar.Streak(days, Today, 2);
        var again = StudyCalendar.Streak(days, Today, 2);

        again.FreezesLeftThisMonth.ShouldBe(first.FreezesLeftThisMonth);
        again.Frozen.Count.ShouldBe(1);
    }

    [Fact]
    public void NoFreezeLeft_TheStreakEnds()
    {
        // Two missed days in October, one freeze a month
        var streak = StudyCalendar.Streak(Days(0, 2, 4, 5), Today, freezesPerMonth: 1);

        streak.Current.ShouldBe(2);
        streak.Frozen.ShouldBe([Today.AddDays(-1)]);
        streak.FreezesLeftThisMonth.ShouldBe(0);
    }

    [Fact]
    public void GapsBeforeTheFirstStudyDay_CostNothing()
    {
        var streak = StudyCalendar.Streak(Days(0), Today, freezesPerMonth: 2);

        streak.Current.ShouldBe(1);
        streak.Frozen.ShouldBeEmpty();
        streak.FreezesLeftThisMonth.ShouldBe(2);
    }

    [Fact]
    public void WithoutFreezes_ItIsTheOldStreak()
    {
        var days = Days(0, 1, 3, 4);

        StudyCalendar.Streak(days, Today, 0).Current.ShouldBe(StudyCalendar.Streak(days, Today).Current);
    }

    [Fact]
    public void Goal_ADayCountsOnlyWhenReached()
    {
        var times = Enumerable.Range(0, 5).Select(i => new DateTime(2026, 10, 30, 8, i, 0, DateTimeKind.Utc))
            .Append(new DateTime(2026, 10, 29, 8, 0, 0, DateTimeKind.Utc));

        StudyCalendar.DaysWithGoal(times, TimeZoneInfo.Utc, goal: 5).ShouldBe([Today]);
    }
}
