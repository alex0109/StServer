using System.Text.Json;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Notification;
using Moq;
using Shouldly;

namespace UnitTests.Notifications;

public class NotificationDismissTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<INotificationRepository> _repo = new();
    private readonly NotificationService _sut;

    public NotificationDismissTests()
    {
        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(_userId);
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));

        _sut = new NotificationService(_repo.Object, Mock.Of<IStudyActivityService>(), Mock.Of<IUserProfileService>(),
            Mock.Of<IUserTimeZoneProvider>(), user.Object, time.Object);
    }

    [Fact]
    public async Task Dismissing_HidesIt_AndItNoLongerCountsAsUnread()
    {
        var notification = new UserNotification
        {
            Id = Guid.NewGuid(), UserId = _userId, Type = NotificationType.StreakAtRisk, PeriodKey = "2026-11-21",
            Data = JsonDocument.Parse("{}"), CreatedAt = Now.AddHours(-2),
        };
        _repo.Setup(x => x.GetAsync(notification.Id, _userId)).ReturnsAsync(notification);

        (await _sut.DismissAsync(notification.Id)).ShouldBeTrue();

        // Kept, not deleted: the same reminder for the same day must not be made again
        notification.DismissedAt.ShouldBe(Now);
        notification.ReadAt.ShouldBe(Now);
        _repo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SomeoneElsesNotification_IsNotFound()
    {
        (await _sut.DismissAsync(Guid.NewGuid())).ShouldBeFalse();

        _repo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DismissAll_HidesTheWholeFeed()
    {
        await _sut.DismissAllAsync();

        _repo.Verify(x => x.DismissAllAsync(_userId, Now), Times.Once);
    }
}
