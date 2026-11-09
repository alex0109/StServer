using Application.Ai;
using Application.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Application.Limits;
using Application.Services;
using Domain.Utility.Ai;
using Domain.Utility.Subscription;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Billing;

public class BillingCatalogTests
{
    private readonly Mock<IBillingProvider> _provider = new();

    private BillingCatalog Catalog() => new(
        [_provider.Object], new ContentLimitsOptions(), new AiOptions(), NullLogger<BillingCatalog>.Instance);

    [Fact]
    public async Task Prices_GoToTheirTiers_FreeHasNone()
    {
        _provider.Setup(x => x.IsConfigured).Returns(true);
        _provider.Setup(x => x.GetPricesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new BillingPrice(BillingPlan.Duo, BillingInterval.Year, 110000, "nok",
                new Dictionary<string, long> { ["nok"] = 110000, ["usd"] = 11000 }),
            new BillingPrice(BillingPlan.Plus, BillingInterval.Year, 70000, "nok"),
            new BillingPrice(BillingPlan.Plus, BillingInterval.Month, 7000, "nok"),
        ]);

        var plans = await Catalog().GetPlansAsync(TestContext.Current.CancellationToken);

        plans.CheckoutAvailable.ShouldBeTrue();
        plans.Plans.Select(x => x.Tier).ShouldBe([SubscriptionTier.Free, SubscriptionTier.Plus, SubscriptionTier.Duo]);
        plans.Plans[0].Prices.ShouldBeEmpty();
        plans.Plans[1].Prices.Select(x => x.Interval).ShouldBe([BillingInterval.Month, BillingInterval.Year]);
        // A price without other currencies still lists its own
        plans.Plans[1].Prices[0].Amounts.ShouldBe(new Dictionary<string, long> { ["nok"] = 7000 });
        plans.Plans[2].Prices.ShouldHaveSingleItem().Amounts["usd"].ShouldBe(11000);
        plans.Plans[2].AiPerMonth.ShouldBe(plans.Plans[1].AiPerMonth);
        plans.Plans[1].AiPerMonth[AiFeature.QuestionGeneration].ShouldBe(AiQuotaDefaults.Get(SubscriptionTier.Plus, AiFeature.QuestionGeneration));
    }

    [Fact]
    public async Task EveryPlan_HasTheSameRoomForContent()
    {
        var plans = await Catalog().GetPlansAsync(TestContext.Current.CancellationToken);

        plans.Plans.Select(x => x.Limits).Distinct().ShouldHaveSingleItem().ShouldBe(ContentLimitDefaults.Everyone);
    }

    [Fact]
    public async Task WhenStripeIsDown_ThePlansStillShowWhatTheyGive()
    {
        _provider.Setup(x => x.IsConfigured).Returns(true);
        _provider.Setup(x => x.GetPricesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BillingUnavailableException("Stripe answered 500"));

        var plans = await Catalog().GetPlansAsync(TestContext.Current.CancellationToken);

        plans.CheckoutAvailable.ShouldBeFalse();
        plans.Plans.Count.ShouldBe(3);
        plans.Plans[1].AiPerMonth[AiFeature.AnswerGrading].ShouldBe(AiQuotaDefaults.Get(SubscriptionTier.Plus, AiFeature.AnswerGrading));
    }

    [Fact]
    public async Task WithoutAProvider_NothingIsAskedFor()
    {
        (await Catalog().GetPlansAsync(TestContext.Current.CancellationToken)).CheckoutAvailable.ShouldBeFalse();

        _provider.Verify(x => x.GetPricesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
