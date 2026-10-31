using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Billing;
using Application.DTOs.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Admin;
using Domain.Utility.Subscription;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class SubscriptionRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private static UserSubscription Plus(SubscriptionStatus status, DateTime? periodEnd, DateTime? trialEnd = null) => new()
    {
        UserId = Guid.NewGuid(),
        Tier = SubscriptionTier.Plus,
        Status = status,
        CurrentPeriodEnd = periodEnd,
        TrialEndsAt = trialEnd,
        CreatedAt = Now.AddMonths(-3),
    };

    [Fact]
    public void PastDue_Canceled_Expired_LowerThePlanAtTheRightMoment()
    {
        var periodEnd = Now.AddDays(-2);

        // A failed payment keeps the plan for the grace days after the period
        SubscriptionRules.EffectiveTier(Plus(SubscriptionStatus.PastDue, periodEnd), Now).ShouldBe(SubscriptionTier.Plus);
        SubscriptionRules.EffectiveTier(Plus(SubscriptionStatus.PastDue, periodEnd), Now.AddDays(6)).ShouldBe(SubscriptionTier.Free);

        // Canceled keeps what was paid for, to the end of the period
        SubscriptionRules.EffectiveTier(Plus(SubscriptionStatus.Canceled, Now.AddDays(10)), Now).ShouldBe(SubscriptionTier.Plus);
        SubscriptionRules.EffectiveTier(Plus(SubscriptionStatus.Canceled, periodEnd), Now).ShouldBe(SubscriptionTier.Free);

        SubscriptionRules.EffectiveTier(Plus(SubscriptionStatus.Expired, Now.AddDays(10)), Now).ShouldBe(SubscriptionTier.Free);
    }

    [Fact]
    public void Trial_EndsOnItsOwnDate()
    {
        var trial = Plus(SubscriptionStatus.Trialing, Now.AddDays(30), trialEnd: Now.AddDays(1));

        SubscriptionRules.EffectiveTier(trial, Now).ShouldBe(SubscriptionTier.Plus);
        SubscriptionRules.EffectiveTier(trial, Now.AddDays(2)).ShouldBe(SubscriptionTier.Free);
    }

    [Fact]
    public void RowsFromBeforeStatuses_WorkAsBefore()
    {
        // Existing rows got Active and Manual from the migration
        SubscriptionRules.EffectiveTier(null, Now).ShouldBe(SubscriptionTier.Free);
        SubscriptionRules.EffectiveTier(Plus(SubscriptionStatus.Active, null), Now).ShouldBe(SubscriptionTier.Plus);
        SubscriptionRules.EffectiveTier(Plus(SubscriptionStatus.Active, Now.AddMinutes(-1)), Now).ShouldBe(SubscriptionTier.Free);
    }
}

public class BillingWebhookTests
{
    private const string Secret = "whsec_test_only";
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeBillingRepository _repo = new();
    private readonly Mock<IBillingJobs> _jobs = new();
    private readonly FakeBillingProvider _provider = new();
    private readonly BillingService _sut;

    public BillingWebhookTests()
    {
        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(_userId);
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));

        _sut = new BillingService(
            [_provider], _repo, _jobs.Object, user.Object, time.Object, NullLogger<BillingService>.Instance);
    }

    private static string Sign(byte[] body) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body)).ToLowerInvariant();

    private byte[] Event(string id, SubscriptionStatus status, DateTime occurredAt, DateTime? periodEnd = null) =>
        JsonSerializer.SerializeToUtf8Bytes(new BillingEvent(
            id, "subscription.updated", occurredAt, _userId, "ctm_1", "sub_1",
            SubscriptionTier.Plus, status, Now.AddDays(-1), periodEnd ?? Now.AddDays(29), null, false));

    private async Task<WebhookOutcome> Deliver(byte[] body, string? signature = null)
    {
        var outcome = await _sut.ReceiveWebhookAsync("fake", signature ?? Sign(body), body);
        foreach (var stored in _repo.Events.Where(x => x.ProcessedAt is null).ToList())
            await _sut.ProcessEventAsync(stored.Id);
        return outcome;
    }

    [Fact]
    public async Task BadSignature_ChangesNothing()
    {
        var body = Event("evt_1", SubscriptionStatus.Active, Now);

        (await Deliver(body, signature: "00ff")).ShouldBe(WebhookOutcome.BadSignature);
        (await Deliver(body, signature: "not hex")).ShouldBe(WebhookOutcome.BadSignature);

        _repo.Events.ShouldBeEmpty();
        _repo.Subscriptions.ShouldBeEmpty();
        _jobs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnknownProvider_IsNotFound()
    {
        var body = Event("evt_1", SubscriptionStatus.Active, Now);

        (await _sut.ReceiveWebhookAsync("stripe", Sign(body), body)).ShouldBe(WebhookOutcome.UnknownProvider);
    }

    [Fact]
    public async Task AValidEvent_GivesThePlan_InTheBackground()
    {
        (await Deliver(Event("evt_1", SubscriptionStatus.Active, Now))).ShouldBe(WebhookOutcome.Accepted);

        var subscription = _repo.Subscriptions.ShouldHaveSingleItem();
        subscription.Tier.ShouldBe(SubscriptionTier.Plus);
        subscription.Provider.ShouldBe(SubscriptionProvider.Paddle);
        subscription.ProviderSubscriptionId.ShouldBe("sub_1");
        _jobs.Verify(x => x.ProcessEvent(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task TheSameEventTwice_IsAppliedOnce()
    {
        var body = Event("evt_1", SubscriptionStatus.Active, Now);

        await Deliver(body);
        _repo.Subscriptions.Single().Tier = SubscriptionTier.Free;
        (await Deliver(body)).ShouldBe(WebhookOutcome.Duplicate);

        _repo.Events.Count.ShouldBe(1);
        _repo.Subscriptions.Single().Tier.ShouldBe(SubscriptionTier.Free);
    }

    [Fact]
    public async Task AnOlderEvent_DoesNotUndoANewerState()
    {
        await Deliver(Event("evt_2", SubscriptionStatus.Canceled, Now));
        await Deliver(Event("evt_1", SubscriptionStatus.Active, Now.AddHours(-1)));

        _repo.Subscriptions.Single().Status.ShouldBe(SubscriptionStatus.Canceled);
        _repo.Events.Single(x => x.ProviderEventId == "evt_1").Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task Checkout_WithoutAProvider_IsUnavailable()
    {
        var service = new BillingService(
            [], _repo, _jobs.Object, new Mock<IUserContext>().Object, TimeProvider.System, NullLogger<BillingService>.Instance);

        await Should.ThrowAsync<BillingUnavailableException>(() => service.CreateCheckoutAsync(BillingPlan.Plus, BillingInterval.Month, "uk"));
        (await service.GetStatusAsync()).CheckoutAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Checkout_WhileAPaidPlanRuns_IsAConflict()
    {
        await Deliver(Event("evt_1", SubscriptionStatus.Active, Now));

        await Should.ThrowAsync<ConflictException>(() => _sut.CreateCheckoutAsync(BillingPlan.Pro, BillingInterval.Year, "uk"));
        _provider.Checkouts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Checkout_AfterAPaidPlanEnded_KeepsTheSameCustomer()
    {
        await Deliver(Event("evt_1", SubscriptionStatus.Expired, Now));

        var url = await _sut.CreateCheckoutAsync(BillingPlan.Pro, BillingInterval.Year, "nb");

        url.ShouldBe("https://checkout.example/Pro/Year");
        var checkout = _provider.Checkouts.ShouldHaveSingleItem();
        checkout.UserId.ShouldBe(_userId);
        checkout.CustomerId.ShouldBe("ctm_1");
        checkout.Language.ShouldBe("nb");
    }

    [Fact]
    public async Task Checkout_OverAHandMadeGrant_IsAllowed()
    {
        _repo.Subscriptions.Add(new UserSubscription
        {
            UserId = _userId,
            Tier = SubscriptionTier.Plus,
            GrantReason = "Beta tester",
            CreatedAt = Now.AddDays(-10),
        });

        await _sut.CreateCheckoutAsync(BillingPlan.Plus, BillingInterval.Month, null);

        _provider.Checkouts.ShouldHaveSingleItem().CustomerId.ShouldBeNull();
    }

    [Fact]
    public async Task Checkout_UnknownPlan_IsABadRequest()
    {
        await Should.ThrowAsync<BadRequestException>(() => _sut.CreateCheckoutAsync((BillingPlan)7, BillingInterval.Month, null));
        await Should.ThrowAsync<BadRequestException>(() => _sut.CreateCheckoutAsync(BillingPlan.Plus, (BillingInterval)0, null));
    }

    [Fact]
    public async Task Portal_OpensOnlyForAPaidPlansCustomer()
    {
        await Should.ThrowAsync<BadRequestException>(() => _sut.CreatePortalAsync("uk"));
        (await _sut.GetStatusAsync()).CanManage.ShouldBeFalse();

        await Deliver(Event("evt_1", SubscriptionStatus.Active, Now));

        (await _sut.CreatePortalAsync("uk")).ShouldBe("https://portal.example/ctm_1");
        (await _sut.GetStatusAsync()).CanManage.ShouldBeTrue();
    }

    [Fact]
    public void Signature_ComparesTheWholeHmac()
    {
        var body = "{}"u8.ToArray();

        WebhookSignature.IsValidHmacSha256(Secret, body, Sign(body)).ShouldBeTrue();
        WebhookSignature.IsValidHmacSha256(Secret, body, Sign(body)[..^2]).ShouldBeFalse();
        WebhookSignature.IsValidHmacSha256("", body, Sign(body)).ShouldBeFalse();
    }

    // Stands in for a real provider: signs like one and records what the service asks for
    private sealed class FakeBillingProvider : IBillingProvider
    {
        public List<BillingCheckout> Checkouts { get; } = [];

        public SubscriptionProvider Provider => SubscriptionProvider.Paddle;
        public string Name => "fake";
        public string SignatureHeader => "X-Signature";
        public bool IsConfigured => true;

        public bool VerifyWebhook(string? signatureHeader, byte[] rawBody, DateTime now) =>
            WebhookSignature.IsValidHmacSha256(Secret, rawBody, signatureHeader);

        public BillingEvent? ParseEvent(byte[] rawBody) => JsonSerializer.Deserialize<BillingEvent>(rawBody);

        public Task<string> CreateCheckoutUrlAsync(BillingCheckout checkout, CancellationToken cancellationToken = default)
        {
            Checkouts.Add(checkout);
            return Task.FromResult($"https://checkout.example/{checkout.Plan}/{checkout.Interval}");
        }

        public Task<string> CreatePortalUrlAsync(string customerId, string? language, CancellationToken cancellationToken = default) =>
            Task.FromResult($"https://portal.example/{customerId}");

        public Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<BillingPrice>> GetPricesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BillingPrice>>([]);
    }
}

public class SubscriptionGrantTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _adminId = Guid.NewGuid();
    private readonly FakeBillingRepository _repo = new();
    private readonly Mock<IAdminAudit> _audit = new();
    private readonly Mock<IAdminAlerts> _alerts = new();
    private readonly SubscriptionAdminService _sut;

    public SubscriptionGrantTests()
    {
        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(_adminId);
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));

        _sut = new SubscriptionAdminService(_repo, _audit.Object, _alerts.Object, user.Object, time.Object);
    }

    [Fact]
    public async Task Grant_IsAudited_AndEndsByItself()
    {
        var userId = Guid.NewGuid();

        var granted = await _sut.GrantAsync(new SubscriptionGrantDto
        {
            UserId = userId,
            Tier = SubscriptionTier.Plus,
            Until = Now.AddDays(30),
            Reason = "Beta tester",
        });

        granted.EffectiveTier.ShouldBe(SubscriptionTier.Plus);
        var row = _repo.Subscriptions.Single();
        row.GrantedByAdminId.ShouldBe(_adminId);
        SubscriptionRules.EffectiveTier(row, Now.AddDays(31)).ShouldBe(SubscriptionTier.Free);
        _audit.Verify(x => x.RecordAsync(AdminAction.GrantSubscription, userId, It.Is<string>(d => d.Contains("Beta tester"))), Times.Once);
        _alerts.Verify(x => x.SubscriptionChangedAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Revoke_OnlyHandMadeGrants_AndIsAudited()
    {
        var userId = Guid.NewGuid();
        await _sut.GrantAsync(new SubscriptionGrantDto { UserId = userId, Reason = "Beta tester" });

        (await _sut.RevokeAsync(userId)).ShouldBeTrue();
        _repo.Subscriptions.ShouldBeEmpty();
        _audit.Verify(x => x.RecordAsync(AdminAction.RevokeSubscription, userId, It.IsAny<string>()), Times.Once);

        var paying = Guid.NewGuid();
        _repo.Subscriptions.Add(new UserSubscription
        {
            UserId = paying,
            Tier = SubscriptionTier.Plus,
            Provider = SubscriptionProvider.Paddle,
            CurrentPeriodEnd = Now.AddDays(10),
            CreatedAt = Now,
        });
        (await _sut.RevokeAsync(paying)).ShouldBeFalse();
        await Should.ThrowAsync<ConflictException>(() =>
            _sut.GrantAsync(new SubscriptionGrantDto { UserId = paying, Reason = "Beta tester" }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    public async Task Grant_NeedsAReason(string reason)
    {
        await Should.ThrowAsync<BadRequestException>(() =>
            _sut.GrantAsync(new SubscriptionGrantDto { UserId = Guid.NewGuid(), Reason = reason }));

        _audit.VerifyNoOtherCalls();
    }
}

internal sealed class FakeBillingRepository : IBillingRepository
{
    public List<UserSubscription> Subscriptions { get; } = [];
    public List<SubscriptionEvent> Events { get; } = [];

    public Task<UserSubscription?> GetSubscriptionAsync(Guid userId) =>
        Task.FromResult(Subscriptions.FirstOrDefault(x => x.UserId == userId));

    public Task<UserSubscription?> FindByProviderSubscriptionAsync(SubscriptionProvider provider, string subscriptionId) =>
        Task.FromResult(Subscriptions.FirstOrDefault(x => x.Provider == provider && x.ProviderSubscriptionId == subscriptionId));

    public Task AddSubscriptionAsync(UserSubscription subscription)
    {
        Subscriptions.Add(subscription);
        return Task.CompletedTask;
    }

    public void RemoveSubscription(UserSubscription subscription) => Subscriptions.Remove(subscription);

    public Task<List<UserSubscription>> ListSubscriptionsAsync(int take) => Task.FromResult(Subscriptions.Take(take).ToList());

    public Task<bool> TryAddEventAsync(SubscriptionEvent subscriptionEvent)
    {
        if (Events.Any(x => x.Provider == subscriptionEvent.Provider && x.ProviderEventId == subscriptionEvent.ProviderEventId))
            return Task.FromResult(false);

        Events.Add(subscriptionEvent);
        return Task.FromResult(true);
    }

    public Task<SubscriptionEvent?> GetEventAsync(Guid eventId) => Task.FromResult(Events.FirstOrDefault(x => x.Id == eventId));

    public Task SaveChangesAsync() => Task.CompletedTask;
}
