using Application.Ai;
using Application.Constants;
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
    private readonly Mock<IAiUsageRepository> _repo = new();
    private readonly Mock<ILimitLock> _limitLock = new();
    private readonly Mock<ILimitLockScope> _limitLockScope = new();
    private readonly Mock<TimeProvider> _time = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
    private readonly AiOptions _options = new();

    public AiUsageServiceTests()
    {
        _limitLock
            .Setup(x => x.AcquireAsync(It.IsAny<LimitScope>(), It.IsAny<Guid>()))
            .ReturnsAsync(_limitLockScope.Object);
        _time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(_now));
    }

    private AiUsageService Create() => new(_repo.Object, _limitLock.Object, _options, _time.Object);

    private void Used(AiFeature feature, int count) =>
        _repo.Setup(x => x.CountAsync(_userId, feature, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)))
            .ReturnsAsync(count);

    [Fact]
    public async Task ReserveAsync_UnderQuota_AddsPendingUsageUnderLock()
    {
        Used(AiFeature.SummaryGeneration, 2);

        var id = await Create().ReserveAsync(_userId, AiFeature.SummaryGeneration);

        id.ShouldNotBe(Guid.Empty);
        _limitLock.Verify(x => x.AcquireAsync(LimitScope.AiUsage, _userId), Times.Once);
        _repo.Verify(x => x.AddAsync(It.Is<AiUsage>(u =>
            u.Id == id && u.Status == AiUsageStatus.Pending && u.Feature == AiFeature.SummaryGeneration)), Times.Once);
        _limitLockScope.Verify(x => x.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task ReserveAsync_QuotaUsed_ThrowsAndAddsNothing()
    {
        // Free plan has 3 summaries a month
        Used(AiFeature.SummaryGeneration, 3);

        await Should.ThrowAsync<AiQuotaExceededException>(
            () => Create().ReserveAsync(_userId, AiFeature.SummaryGeneration));

        _repo.Verify(x => x.AddAsync(It.IsAny<AiUsage>()), Times.Never);
        _limitLockScope.Verify(x => x.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task ReserveAsync_ActiveSubscription_UsesItsQuota()
    {
        _repo.Setup(x => x.GetSubscriptionAsync(_userId)).ReturnsAsync(new UserSubscription
        {
            UserId = _userId,
            Tier = SubscriptionTier.Plus,
            CurrentPeriodEnd = _now.AddDays(3),
            CreatedAt = _now,
        });
        Used(AiFeature.SummaryGeneration, 3);

        await Create().ReserveAsync(_userId, AiFeature.SummaryGeneration);

        _repo.Verify(x => x.AddAsync(It.IsAny<AiUsage>()), Times.Once);
    }

    [Fact]
    public async Task ReserveAsync_ExpiredSubscription_FallsBackToFree()
    {
        _repo.Setup(x => x.GetSubscriptionAsync(_userId)).ReturnsAsync(new UserSubscription
        {
            UserId = _userId,
            Tier = SubscriptionTier.Pro,
            CurrentPeriodEnd = _now.AddDays(-1),
            CreatedAt = _now,
        });
        Used(AiFeature.SummaryGeneration, 3);

        await Should.ThrowAsync<AiQuotaExceededException>(
            () => Create().ReserveAsync(_userId, AiFeature.SummaryGeneration));
    }

    [Fact]
    public async Task ReserveAsync_ConfiguredQuota_OverridesDefault()
    {
        _options.Quotas = new() { [SubscriptionTier.Free] = new() { [AiFeature.SummaryGeneration] = 0 } };
        Used(AiFeature.SummaryGeneration, 0);

        await Should.ThrowAsync<AiQuotaExceededException>(
            () => Create().ReserveAsync(_userId, AiFeature.SummaryGeneration));
    }

    [Fact]
    public async Task CompleteAsync_StoresProviderAndTokens()
    {
        var usage = new AiUsage { Id = Guid.NewGuid(), UserId = _userId, Status = AiUsageStatus.Pending, CreatedAt = _now };
        _repo.Setup(x => x.GetByIdAsync(usage.Id)).ReturnsAsync(usage);

        await Create().CompleteAsync(usage.Id, new AiCompletion
        {
            Text = "{}", Provider = "Anthropic", Model = "claude-opus-5-5", InputTokens = 120, OutputTokens = 40,
        });

        usage.Status.ShouldBe(AiUsageStatus.Succeeded);
        usage.Provider.ShouldBe("Anthropic");
        usage.InputTokens.ShouldBe(120);
        usage.CompletedAt.ShouldBe(_now);
    }

    [Fact]
    public async Task GetUsageAsync_ReturnsEveryFeatureWithLimits()
    {
        _repo.Setup(x => x.CountByFeatureAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<AiFeature, int> { [AiFeature.AnswerGrading] = 7 });

        var usage = await Create().GetUsageAsync(_userId);

        usage.Tier.ShouldBe(SubscriptionTier.Free);
        usage.ResetsAt.ShouldBe(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        usage.Features.Count.ShouldBe(Enum.GetValues<AiFeature>().Length);
        usage.Features.Single(x => x.Feature == AiFeature.AnswerGrading).Used.ShouldBe(7);
        usage.Features.Single(x => x.Feature == AiFeature.AnswerGrading).Limit.ShouldBe(30);
    }
}
