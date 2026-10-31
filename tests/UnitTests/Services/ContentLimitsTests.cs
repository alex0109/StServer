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
    public async Task NoSubscription_IsFree()
    {
        (await Limits().ForUserAsync(_userId)).ShouldBe(ContentLimitDefaults.Free);
    }

    [Fact]
    public async Task ExpiredPlus_FallsBackToFree()
    {
        OnPlan(SubscriptionTier.Plus, until: Now.AddDays(-1));

        (await Limits().ForUserAsync(_userId)).ShouldBe(ContentLimitDefaults.Free);
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
        limits.EntriesPerDictionary.ShouldBe(ContentLimitDefaults.Plus.EntriesPerDictionary);
    }

    [Fact]
    public async Task Pro_HasNoMaterialLimit()
    {
        OnPlan(SubscriptionTier.Pro);
        _materials.Setup(x => x.CountOwnByUserIdAsync(_userId)).ReturnsAsync(100_000);

        await Should.NotThrowAsync(() => Limits().EnsureMaterialPlaceAsync(_userId, libraryCopy: false));
    }

    [Fact]
    public async Task LibraryCopies_DoNotTakeOwnPlaces_ByDefault()
    {
        _materials.Setup(x => x.CountOwnByUserIdAsync(_userId)).ReturnsAsync(ContentLimitDefaults.Free.Materials!.Value);
        _materials.Setup(x => x.CountLibraryCopiesAsync(_userId)).ReturnsAsync(0);

        await Should.NotThrowAsync(() => Limits().EnsureMaterialPlaceAsync(_userId, libraryCopy: true));
        await Should.ThrowAsync<LimitExceededException>(() => Limits().EnsureMaterialPlaceAsync(_userId, libraryCopy: false));
    }

    [Fact]
    public async Task LibraryCopies_ShareTheMaterialLimit_WhenConfigured()
    {
        _materials.Setup(x => x.CountByUserIdAsync(_userId)).ReturnsAsync(ContentLimitDefaults.Free.Materials!.Value);

        await Should.ThrowAsync<LimitExceededException>(() =>
            Limits(new ContentLimitsOptions { CountLibraryImports = true }).EnsureMaterialPlaceAsync(_userId, libraryCopy: true));
    }
}
