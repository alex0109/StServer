using System.Text.Json;
using Application.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Utility.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using UnitTests.Ai;

namespace UnitTests.Services;

public class CreditServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeCreditRepository _credits = new();
    private readonly FakeBillingRepository _billing = new();
    private readonly Mock<IBillingProvider> _provider = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly CreditService _sut;

    public CreditServiceTests()
    {
        _user.Setup(x => x.UserId).Returns(_userId);
        _user.Setup(x => x.Email).Returns("ann@example.com");
        _provider.Setup(x => x.IsConfigured).Returns(true);
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));

        _sut = new CreditService(_credits, _billing, [_provider.Object], new FakeLimitLock(), TestPricing.Shipped(),
            _user.Object, time.Object, NullLogger<CreditService>.Instance);
    }

    private CreditEvent Purchase(string eventId, string pack = "medium", Guid? user = null) => new(
        eventId, BillingEventTypes.CheckoutCompleted, Now, CreditEventKind.Purchase, user ?? _userId, pack, "pi_1", "cs_1", 500, 0);

    private static CreditEvent Refund(string eventId, long refunded, long amount = 500) => new(
        eventId, BillingEventTypes.ChargeRefunded, Now, CreditEventKind.Refund, null, null, "pi_1", null, amount, refunded);

    [Fact]
    public async Task APurchase_GrantsThePackFromServerConfig_Once()
    {
        (await _sut.ApplyAsync(Purchase("evt_1"))).ShouldBeNull();
        // Stripe delivers again, also under another event id for the same checkout
        (await _sut.ApplyAsync(Purchase("evt_1"))).ShouldNotBeNull();
        (await _sut.ApplyAsync(Purchase("evt_2"))).ShouldNotBeNull();

        _credits.Balance(_userId).ShouldBe(500);
        _credits.Transactions.ShouldHaveSingleItem().PackId.ShouldBe("medium");
    }

    [Fact]
    public async Task AnUnknownPack_GrantsNothing()
    {
        (await _sut.ApplyAsync(Purchase("evt_1", pack: "huge"))).ShouldNotBeNull();

        _credits.Transactions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARefund_TakesBackTheRefundedShare_ThenTheRest()
    {
        await _sut.ApplyAsync(Purchase("evt_1"));

        await _sut.ApplyAsync(Refund("evt_r1", refunded: 250));
        _credits.Balance(_userId).ShouldBe(250);

        await _sut.ApplyAsync(Refund("evt_r2", refunded: 500));
        await _sut.ApplyAsync(Refund("evt_r2", refunded: 500));
        _credits.Balance(_userId).ShouldBe(0);
    }

    [Fact]
    public async Task ARefundOfSpentCredits_StopsAtZero_AndIsLeftForReview()
    {
        await _sut.ApplyAsync(Purchase("evt_1"));
        _credits.Wallets.Single().Balance = 120;

        await _sut.ApplyAsync(Refund("evt_r1", refunded: 500));

        _credits.Balance(_userId).ShouldBe(0);
        _credits.Transactions.Single(x => x.Reason == CreditReason.Refund).Note.ShouldNotBeNull();
    }

    [Fact]
    public async Task ARefundOfASubscription_ChangesNoCredits()
    {
        (await _sut.ApplyAsync(Refund("evt_r1", refunded: 9900, amount: 9900))).ShouldBe("Not a credit purchase");
    }

    [Fact]
    public async Task Checkout_SendsTheServersPack_AndRefusesAnUnknownOne()
    {
        _provider.Setup(x => x.CreateCreditCheckoutUrlAsync(It.IsAny<CreditCheckout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://checkout.example");

        await _sut.CreateCheckoutAsync("large", "uk");

        _provider.Verify(x => x.CreateCreditCheckoutUrlAsync(
            It.Is<CreditCheckout>(c => c.PackId == "large" && c.Credits == 1050 && c.Price == 10 && c.UserId == _userId), It.IsAny<CancellationToken>()));
        await Should.ThrowAsync<BadRequestException>(() => _sut.CreateCheckoutAsync("free-money", null));
    }

    [Fact]
    public async Task AutoUse_IsSavedOnTheWallet()
    {
        (await _sut.SetAutoUseAsync(true)).AutoUseForStudy.ShouldBeTrue();
        (await _sut.GetWalletAsync()).AutoUseForStudy.ShouldBeTrue();
    }

    [Fact]
    public async Task TheSameWebhook_DeliveredThreeTimes_GrantsOnce()
    {
        var jobs = new Mock<IBillingJobs>();
        var provider = new Mock<IBillingProvider>();
        provider.Setup(x => x.Name).Returns("stripe");
        provider.Setup(x => x.IsConfigured).Returns(true);
        provider.Setup(x => x.Provider).Returns(Domain.Utility.Subscription.SubscriptionProvider.Stripe);
        provider.Setup(x => x.VerifyWebhook(It.IsAny<string?>(), It.IsAny<byte[]>(), It.IsAny<DateTime>())).Returns(true);
        provider.Setup(x => x.ParseCreditEvent(It.IsAny<byte[]>())).Returns(Purchase("evt_1"));
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));
        var billing = new BillingService([provider.Object], _billing, jobs.Object, _user.Object, time.Object,
            NullLogger<BillingService>.Instance, _sut);
        var body = JsonSerializer.SerializeToUtf8Bytes(new { id = "evt_1" });

        for (var i = 0; i < 3; i++)
        {
            await billing.ReceiveWebhookAsync("stripe", "sig", body);
            foreach (var stored in _billing.Events.Where(x => x.ProcessedAt is null).ToList())
                await billing.ProcessEventAsync(stored.Id);
        }

        _billing.Events.ShouldHaveSingleItem();
        _credits.Balance(_userId).ShouldBe(500);
    }
}
