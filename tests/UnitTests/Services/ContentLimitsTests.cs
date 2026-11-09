using Application.Exceptions;
using Application.Interfaces;
using Application.Limits;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Subscription;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class ContentLimitsTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IAiUsageRepository> _subscriptions = new();
    private readonly Mock<IMaterialRepository> _materials = new();

    private ContentLimits Limits(ContentLimitsOptions? options = null)
    {
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));

        return new ContentLimits(
            new SubscriptionTierProvider(_subscriptions.Object, time.Object),
            _materials.Object,
            options ?? new ContentLimitsOptions());
    }

    private void OnPlan(SubscriptionTier tier, DateTime? until = null) =>
        _subscriptions.Setup(x => x.GetSubscriptionAsync(_userId)).ReturnsAsync(new UserSubscription
        {
            UserId = _userId,
            Tier = tier,
            CurrentPeriodEnd = until,
            CreatedAt = Now,
        });

    [Fact]
    public async Task NoSubscription_HasTheSharedRoom()
    {
        (await Limits().ForUserAsync(_userId)).ShouldBe(ContentLimitDefaults.Everyone);
    }

    // Content isn't sold, so a plan that ends locks nothing
    [Theory]
    [InlineData(SubscriptionTier.Plus, 10)]
    [InlineData(SubscriptionTier.Plus, -1)]
    [InlineData(SubscriptionTier.Duo, 10)]
    [InlineData(SubscriptionTier.Pro, 10)]
    public async Task EveryPlan_ActiveOrEnded_HasTheSameRoom(SubscriptionTier tier, int daysLeft)
    {
        OnPlan(tier, until: Now.AddDays(daysLeft));

        (await Limits().ForUserAsync(_userId)).ShouldBe(ContentLimitDefaults.Everyone);
    }

    [Fact]
    public async Task Configuration_OverridesDefaults_AndMinusOneMeansNoLimit()
    {
        OnPlan(SubscriptionTier.Plus);
        var options = new ContentLimitsOptions
        {
            Tiers = new() { [SubscriptionTier.Plus] = new TierLimitsConfig { Materials = 999, Tags = -1 } },
        };

        var limits = await Limits(options).ForUserAsync(_userId);

        limits.Materials.ShouldBe(999);
        limits.Tags.ShouldBeNull();
        limits.EntriesPerDictionary.ShouldBe(ContentLimitDefaults.Everyone.EntriesPerDictionary);
    }

    [Fact]
    public async Task AtTheCap_APaidPlanGivesNoMore()
    {
        OnPlan(SubscriptionTier.Duo);
        _materials.Setup(x => x.CountOwnByUserIdAsync(_userId)).ReturnsAsync(ContentLimitDefaults.Everyone.Materials!.Value);

        await Should.ThrowAsync<LimitExceededException>(() => Limits().EnsureMaterialPlaceAsync(_userId, libraryCopy: false));
    }

    [Fact]
    public async Task LibraryCopies_DoNotTakeOwnPlaces_ByDefault()
    {
        _materials.Setup(x => x.CountOwnByUserIdAsync(_userId)).ReturnsAsync(ContentLimitDefaults.Everyone.Materials!.Value);
        _materials.Setup(x => x.CountLibraryCopiesAsync(_userId)).ReturnsAsync(0);

        await Should.NotThrowAsync(() => Limits().EnsureMaterialPlaceAsync(_userId, libraryCopy: true));
        await Should.ThrowAsync<LimitExceededException>(() => Limits().EnsureMaterialPlaceAsync(_userId, libraryCopy: false));
    }

    [Fact]
    public async Task LibraryCopies_ShareTheMaterialLimit_WhenConfigured()
    {
        _materials.Setup(x => x.CountByUserIdAsync(_userId)).ReturnsAsync(ContentLimitDefaults.Everyone.Materials!.Value);

        await Should.ThrowAsync<LimitExceededException>(() =>
            Limits(new ContentLimitsOptions { CountLibraryImports = true }).EnsureMaterialPlaceAsync(_userId, libraryCopy: true));
    }
}
