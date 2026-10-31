using Application.Constants;
using Application.DTOs.Feedback;
using Application.Email;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Notifications;

public class FeedbackServiceTests
{
    private readonly Mock<IEmailSender> _email = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly FakeFeedbackLog _log = new();
    private readonly Mock<ILimitLock> _lock = new();
    private OutgoingEmail? _sent;
    private int _sentCount;

    public FeedbackServiceTests()
    {
        _email.Setup(x => x.IsConfigured).Returns(true);
        _email.Setup(x => x.SendAsync(It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()))
            .Callback<OutgoingEmail, CancellationToken>((e, _) =>
            {
                _sent = e;
                _sentCount++;
            })
            .Returns(Task.CompletedTask);

        var scope = new Mock<ILimitLockScope>();
        _lock.Setup(x => x.AcquireAsync(LimitScope.Feedback, It.IsAny<Guid>())).ReturnsAsync(scope.Object);
    }

    private FeedbackService Create() => new(
        _email.Object,
        new EmailOptions { FeedbackTo = "owner@example.com" },
        _user.Object,
        _log,
        _lock.Object,
        TimeProvider.System,
        NullLogger<FeedbackService>.Instance);

    private static FeedbackCreateDto Message() => new() { Message = "This is a long enough message" };

    [Fact]
    public async Task SameUser_OverMonthlyLimit_IsSilentlyNotSent()
    {
        _user.Setup(x => x.CurrentUserId).Returns(Guid.NewGuid());
        var service = Create();

        for (var i = 0; i < FeedbackService.MaxPerMonth + 2; i++)
            await service.SendAsync(Message(), TestContext.Current.CancellationToken);

        _sentCount.ShouldBe(FeedbackService.MaxPerMonth);
    }

    [Fact]
    public async Task LimitIsPerUser()
    {
        var first = Guid.NewGuid();
        _user.Setup(x => x.CurrentUserId).Returns(first);
        for (var i = 0; i < FeedbackService.MaxPerMonth; i++)
            await Create().SendAsync(Message(), TestContext.Current.CancellationToken);

        _user.Setup(x => x.CurrentUserId).Returns(Guid.NewGuid());
        await Create().SendAsync(Message(), TestContext.Current.CancellationToken);

        _sentCount.ShouldBe(FeedbackService.MaxPerMonth + 1);
    }

    [Fact]
    public async Task Guests_AreCountedByAddress_WithoutStoringIt()
    {
        _user.Setup(x => x.CurrentUserId).Returns((Guid?)null);
        _user.Setup(x => x.ClientIp).Returns("203.0.113.7");

        for (var i = 0; i < FeedbackService.MaxPerMonth + 1; i++)
            await Create().SendAsync(Message(), TestContext.Current.CancellationToken);

        _sentCount.ShouldBe(FeedbackService.MaxPerMonth);
        _log.Keys.ShouldAllBe(key => key.StartsWith("ip:") && !key.Contains("203.0.113.7"));
    }

    [Fact]
    public async Task FailedSend_IsNotCounted()
    {
        _user.Setup(x => x.CurrentUserId).Returns(Guid.NewGuid());
        _email.Setup(x => x.SendAsync(It.IsAny<OutgoingEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Resend is down"));

        await Should.ThrowAsync<InvalidOperationException>(() => Create().SendAsync(Message()));

        _log.Keys.ShouldBeEmpty();
    }

    private sealed class FakeFeedbackLog : IFeedbackLogRepository
    {
        private readonly List<FeedbackSubmission> _items = [];
        private readonly List<FeedbackSubmission> _pending = [];

        public IEnumerable<string> Keys => _items.Select(x => x.SenderKey);

        public Task<int> CountSinceAsync(string senderKey, DateTime since) =>
            Task.FromResult(_items.Count(x => x.SenderKey == senderKey && x.CreatedAt >= since));

        public Task AddAsync(FeedbackSubmission submission)
        {
            _pending.Add(submission);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync()
        {
            _items.AddRange(_pending);
            _pending.Clear();
            return Task.CompletedTask;
        }

        public Task<int> DeleteCreatedBeforeAsync(DateTime createdBefore) =>
            Task.FromResult(_items.RemoveAll(x => x.CreatedAt < createdBefore));
    }

    [Fact]
    public async Task SendsToOwnerWithEncodedText()
    {
        await Create().SendAsync(new FeedbackCreateDto
        {
            Name = "Olena\r\nBcc: spam@example.com",
            Email = "olena@example.com",
            Message = "<script>alert(1)</script> Great app!",
            Category = FeedbackCategory.Idea,
        }, TestContext.Current.CancellationToken);

        _sent.ShouldNotBeNull();
        _sent.To.ShouldBe("owner@example.com");
        _sent.ReplyTo.ShouldBe("olena@example.com");
        _sent.Html.ShouldNotContain("<script>");
        _sent.Html.ShouldContain("&lt;script&gt;");
        _sent.Subject.ShouldNotContain("\n");
        _sent.Subject.ShouldNotContain("\r");
    }

    [Fact]
    public async Task HoneypotFilled_NothingSent()
    {
        await Create().SendAsync(
            new FeedbackCreateDto { Message = "Buy cheap things now!!!", Website = "http://spam" },
            TestContext.Current.CancellationToken);

        _sent.ShouldBeNull();
    }

    [Theory]
    [InlineData("short")]
    [InlineData("          ")]
    public async Task TooShortMessage_Throws(string message)
    {
        await Should.ThrowAsync<BadRequestException>(() => Create().SendAsync(new FeedbackCreateDto { Message = message }));
    }

    [Fact]
    public async Task InvalidEmail_Throws()
    {
        await Should.ThrowAsync<BadRequestException>(() => Create().SendAsync(new FeedbackCreateDto
        {
            Message = "This is a long enough message",
            Email = "not an email",
        }));
    }

    [Fact]
    public async Task EmailNotConfigured_Throws()
    {
        _email.Setup(x => x.IsConfigured).Returns(false);

        await Should.ThrowAsync<EmailUnavailableException>(() => Create().SendAsync(new FeedbackCreateDto
        {
            Message = "This is a long enough message",
        }));
    }
}
