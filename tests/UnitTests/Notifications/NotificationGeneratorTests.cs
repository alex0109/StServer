using Application.DTOs.Notification;
using Application.DTOs.Statistics;
using Application.Email;
using Application.Interfaces;
using Application.Notifications;
using Domain.Entities;
using Domain.Utility.Notification;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Notifications;

public class NotificationGeneratorTests
{
    private readonly Mock<INotificationRepository> _repo = new();
    private readonly Mock<IStudyActivityService> _activity = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly Mock<TimeProvider> _time = new();
    private readonly List<UserNotification> _added = [];

    private readonly UserNotificationSettings _settings = new()
    {
        UserId = Guid.NewGuid(),
        TimeZone = "Europe/Kyiv",
        Language = "uk",
        Email = "user@example.com",
        EmailDailyReminder = true,
        CreatedAt = DateTime.UtcNow,
    };

    public NotificationGeneratorTests()
    {
        _repo.Setup(x => x.TryAddAsync(It.IsAny<UserNotification>()))
            .Callback<UserNotification>(_added.Add)
            .ReturnsAsync(true);
        _email.Setup(x => x.IsConfigured).Returns(true);
        _activity.Setup(x => x.GetTodayAsync(It.IsAny<Guid>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new TodayDto
            {
                LocalDate = "2026-10-21", DueWords = 23, NewWords = 2, Dictionaries = [],
                StreakDays = 3, StudiedToday = false, StreakAtRisk = false,
            });
        _activity.Setup(x => x.GetStreakAsync(It.IsAny<Guid>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new StudyStreakDto { CurrentStreakDays = 3, StudiedToday = false });
    }

    private NotificationGenerator Create() => new(
        _repo.Object, _activity.Object, _email.Object, new NotificationOptions(), new EmailOptions(),
        _time.Object, NullLogger<NotificationGenerator>.Instance);

    // Wednesday; Kyiv is UTC+3 until the last Sunday of October
    private static DateTime KyivTime(int hour) => new(2026, 10, 21, hour - 3, 10, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Morning_MakesDailyReminderAndSendsEmail()
    {
        var created = await Create().GenerateForUserAsync(_settings, KyivTime(9));

        created.ShouldBe(1);
        _added.Single().Type.ShouldBe(NotificationType.DailyReview);
        _added.Single().PeriodKey.ShouldBe("2026-10-21");
        _email.Verify(x => x.SendAsync(
            It.Is<OutgoingEmail>(e => e.To == "user@example.com" && e.Subject.Contains("23 слова")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlreadyMadeToday_DoesNothing()
    {
        _repo.Setup(x => x.ExistsAsync(_settings.UserId, NotificationType.DailyReview, "2026-10-21")).ReturnsAsync(true);

        (await Create().GenerateForUserAsync(_settings, KyivTime(10))).ShouldBe(0);

        _email.Verify(x => x.SendAsync(It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OtherJobMadeItFirst_NoEmail()
    {
        _repo.Setup(x => x.TryAddAsync(It.IsAny<UserNotification>())).ReturnsAsync(false);

        (await Create().GenerateForUserAsync(_settings, KyivTime(9))).ShouldBe(0);

        _email.Verify(x => x.SendAsync(It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmailsOff_OnlyInAppReminder()
    {
        _settings.EmailDailyReminder = false;

        (await Create().GenerateForUserAsync(_settings, KyivTime(9))).ShouldBe(1);

        _email.Verify(x => x.SendAsync(It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Evening_StreakNotStudied_MakesStreakReminder()
    {
        var created = await Create().GenerateForUserAsync(_settings, KyivTime(20));

        created.ShouldBe(1);
        _added.Single().Type.ShouldBe(NotificationType.StreakAtRisk);
        // Streak emails are off for this user
        _email.Verify(x => x.SendAsync(It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Evening_AlreadyStudied_NoStreakReminder()
    {
        _activity.Setup(x => x.GetStreakAsync(It.IsAny<Guid>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new StudyStreakDto { CurrentStreakDays = 4, StudiedToday = true });

        (await Create().GenerateForUserAsync(_settings, KyivTime(21))).ShouldBe(0);
    }

    [Fact]
    public async Task Afternoon_Nothing()
    {
        (await Create().GenerateForUserAsync(_settings, KyivTime(14))).ShouldBe(0);
    }

    [Fact]
    public async Task MondayMorning_MakesWeeklyDigestForLastWeek()
    {
        _activity.Setup(x => x.GetWeekAsync(It.IsAny<Guid>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new WeeklyDigestData("2026-10-19", "2026-10-25", 4, 3, 2, 10, 80, null, 75, 30, []));
        _settings.EmailDailyReminder = false;

        // Monday 26 October, 09:10 in Kyiv (UTC+2 after the clock change)
        await Create().GenerateForUserAsync(_settings, new DateTime(2026, 10, 26, 7, 10, 0, DateTimeKind.Utc));

        _added.ShouldContain(x => x.Type == NotificationType.WeeklyDigest && x.PeriodKey == "2026-W43");
    }
}
