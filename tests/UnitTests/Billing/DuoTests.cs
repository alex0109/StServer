using Application.Billing;
using Application.DTOs.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Subscription;
using Moq;
using Shouldly;
using UnitTests.Services;

namespace UnitTests.Billing;

public class DuoTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly Clock _clock = new();
    private readonly FakeBillingRepository _repo = new();
    private readonly Guid _payer = Guid.NewGuid();
    private readonly Guid _friend = Guid.NewGuid();

    private DuoService As(Guid userId, string email)
    {
        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(userId);
        user.Setup(x => x.Email).Returns(email);
        return new DuoService(_repo, user.Object, _clock);
    }

    private DuoService Payer => As(_payer, "payer@example.com");
    private DuoService Friend => As(_friend, "friend@example.com");

    private UserSubscription Subscribe(Guid userId, SubscriptionTier tier, int days = 30)
    {
        var subscription = new UserSubscription
        {
            UserId = userId,
            Tier = tier,
            Provider = SubscriptionProvider.Stripe,
            CurrentPeriodEnd = _clock.Now.AddDays(days),
            CreatedAt = _clock.Now,
        };
        _repo.Subscriptions.Add(subscription);
        return subscription;
    }

    [Fact]
    public async Task ThePayerSendsALink_AndTheSecondPersonJoins()
    {
        Subscribe(_payer, SubscriptionTier.Duo);

        var invite = await Payer.CreateInviteAsync();
        invite.ExpiresAt.ShouldBe(_clock.Now + DuoService.InviteLifetime);
        // Only a hash of the token is kept
        _repo.Duos.Single().InviteTokenHash.ShouldNotBe(invite.Token);

        (await Friend.PreviewAsync(invite.Token)).ShouldBe(new DuoInvitePreviewDto("payer@example.com", IsOwn: false));
        await Friend.JoinAsync(invite.Token);

        (await Friend.GetAsync()).ShouldBe(new DuoStatusDto(DuoRole.Member, true, false, null, null, _clock.Now, "payer@example.com"));
        var payer = await Payer.GetAsync();
        (payer.Role, payer.Active, payer.CanInvite, payer.MemberEmail, payer.InviteExpiresAt)
            .ShouldBe((DuoRole.Owner, true, false, "friend@example.com", (DateTime?)null));

        // The link worked once
        await Should.ThrowAsync<KeyNotFoundException>(() => As(Guid.NewGuid(), "third@example.com").JoinAsync(invite.Token));
        await Should.ThrowAsync<ConflictException>(() => Payer.CreateInviteAsync());
    }

    [Theory]
    [InlineData(SubscriptionTier.Plus)]
    [InlineData(SubscriptionTier.Free)]
    public async Task WithoutADuo_ThereIsNoLink(SubscriptionTier tier)
    {
        if (tier != SubscriptionTier.Free) Subscribe(_payer, tier);

        await Should.ThrowAsync<BadRequestException>(() => Payer.CreateInviteAsync());
        (await Payer.GetAsync()).Role.ShouldBe(DuoRole.None);
    }

    [Fact]
    public async Task ANewLink_ReplacesTheOld_AndALinkExpires()
    {
        Subscribe(_payer, SubscriptionTier.Duo);

        var first = await Payer.CreateInviteAsync();
        var second = await Payer.CreateInviteAsync();

        await Should.ThrowAsync<KeyNotFoundException>(() => Friend.PreviewAsync(first.Token));

        _clock.Now += DuoService.InviteLifetime + TimeSpan.FromMinutes(1);
        await Should.ThrowAsync<KeyNotFoundException>(() => Friend.JoinAsync(second.Token));
        (await Payer.GetAsync()).InviteExpiresAt.ShouldBeNull();
    }

    [Fact]
    public async Task ACancelledLink_IsGone()
    {
        Subscribe(_payer, SubscriptionTier.Duo);
        var invite = await Payer.CreateInviteAsync();

        await Payer.CancelInviteAsync();

        await Should.ThrowAsync<KeyNotFoundException>(() => Friend.JoinAsync(invite.Token));
    }

    [Fact]
    public async Task TheirOwnLink_OrAPlanOfTheirOwn_KeepsSomeoneOut()
    {
        Subscribe(_payer, SubscriptionTier.Duo);
        var invite = await Payer.CreateInviteAsync();

        (await Payer.PreviewAsync(invite.Token)).IsOwn.ShouldBeTrue();
        await Should.ThrowAsync<BadRequestException>(() => Payer.JoinAsync(invite.Token));

        Subscribe(_friend, SubscriptionTier.Plus);
        await Should.ThrowAsync<ConflictException>(() => Friend.JoinAsync(invite.Token));
        _repo.Duos.Single().MemberUserId.ShouldBeNull();
    }

    [Fact]
    public async Task OnlyOneDuo_AtATime()
    {
        Subscribe(_payer, SubscriptionTier.Duo);
        await Friend.JoinAsync((await Payer.CreateInviteAsync()).Token);

        var otherPayer = Guid.NewGuid();
        Subscribe(otherPayer, SubscriptionTier.Duo);
        var other = await As(otherPayer, "other@example.com").CreateInviteAsync();

        await Should.ThrowAsync<ConflictException>(() => Friend.JoinAsync(other.Token));
    }

    [Fact]
    public async Task WhenThePayerStopsPaying_TheMemberLosesIt_AndGetsItBackOnRenewal()
    {
        var subscription = Subscribe(_payer, SubscriptionTier.Duo);
        await Friend.JoinAsync((await Payer.CreateInviteAsync()).Token);

        subscription.Status = SubscriptionStatus.Expired;

        (await Friend.GetAsync()).Active.ShouldBeFalse();
        var payer = await Payer.GetAsync();
        (payer.Role, payer.Active, payer.CanInvite).ShouldBe((DuoRole.Owner, false, false));
        await Should.ThrowAsync<BadRequestException>(() => Payer.CreateInviteAsync());

        subscription.Status = SubscriptionStatus.Active;
        (await Friend.GetAsync()).Active.ShouldBeTrue();
    }

    [Fact]
    public async Task TheMemberLeaves_OrThePayerRemovesThem()
    {
        Subscribe(_payer, SubscriptionTier.Duo);
        await Friend.JoinAsync((await Payer.CreateInviteAsync()).Token);

        await Friend.LeaveAsync();
        (await Friend.GetAsync()).Role.ShouldBe(DuoRole.None);
        (await Payer.GetAsync()).CanInvite.ShouldBeTrue();

        await Friend.JoinAsync((await Payer.CreateInviteAsync()).Token);
        await Payer.RemoveMemberAsync();
        (await Friend.GetAsync()).Role.ShouldBe(DuoRole.None);
        _repo.Duos.Single().MemberEmail.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task AnUnknownToken_IsNotFound(string token)
    {
        await Should.ThrowAsync<KeyNotFoundException>(() => Friend.PreviewAsync(token));
    }
}

public class DuoTierTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private static UserSubscription Plan(SubscriptionTier tier, int days = 30, SubscriptionStatus status = SubscriptionStatus.Active) => new()
    {
        UserId = Guid.NewGuid(),
        Tier = tier,
        Status = status,
        CurrentPeriodEnd = Now.AddDays(days),
        CreatedAt = Now,
    };

    [Fact]
    public void AMember_HasDuo_ExactlyWhileThePayersDuoIsInForce()
    {
        SubscriptionRules.EffectiveTier(null, Plan(SubscriptionTier.Duo), Now).ShouldBe(SubscriptionTier.Duo);
        SubscriptionRules.EffectiveTier(null, Plan(SubscriptionTier.Duo, days: -1), Now).ShouldBe(SubscriptionTier.Free);
        SubscriptionRules.EffectiveTier(null, Plan(SubscriptionTier.Duo, status: SubscriptionStatus.Expired), Now).ShouldBe(SubscriptionTier.Free);
        // The payer moved to Plus: that is for one person
        SubscriptionRules.EffectiveTier(null, Plan(SubscriptionTier.Plus), Now).ShouldBe(SubscriptionTier.Free);
        SubscriptionRules.EffectiveTier(null, null, Now).ShouldBe(SubscriptionTier.Free);
    }

    [Fact]
    public void APlanOfTheirOwn_Counts()
    {
        SubscriptionRules.EffectiveTier(Plan(SubscriptionTier.Plus), Plan(SubscriptionTier.Duo), Now).ShouldBe(SubscriptionTier.Plus);
    }

    [Fact]
    public async Task TheTierProvider_GivesAMemberDuo_SoLimitsAndAiQuotasFollow()
    {
        var member = Guid.NewGuid();
        var repo = new Mock<IAiUsageRepository>();
        repo.Setup(x => x.GetDuoPayerSubscriptionAsync(member)).ReturnsAsync(Plan(SubscriptionTier.Duo));
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));

        (await new SubscriptionTierProvider(repo.Object, time.Object).GetTierAsync(member)).ShouldBe(SubscriptionTier.Duo);
    }
}
