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
            new BillingPrice(BillingPlan.Pro, BillingInterval.Year, 199000, "nok"),
            new BillingPrice(BillingPlan.Plus, BillingInterval.Year, 99000, "nok"),
            new BillingPrice(BillingPlan.Plus, BillingInterval.Month, 9900, "nok"),
        ]);

        var plans = await Catalog().GetPlansAsync(TestContext.Current.CancellationToken);

        plans.CheckoutAvailable.ShouldBeTrue();
        plans.Plans.Select(x => x.Tier).ShouldBe([SubscriptionTier.Free, SubscriptionTier.Plus, SubscriptionTier.Pro]);
        plans.Plans[0].Prices.ShouldBeEmpty();
        plans.Plans[1].Prices.Select(x => x.Interval).ShouldBe([BillingInterval.Month, BillingInterval.Year]);
        plans.Plans[2].Prices.ShouldHaveSingleItem().Amount.ShouldBe(199000);
        plans.Plans[1].AiPerMonth[AiFeature.QuestionGeneration].ShouldBe(AiQuotaDefaults.Get(SubscriptionTier.Plus, AiFeature.QuestionGeneration));
        plans.Plans[2].Limits.Materials.ShouldBeNull();
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
        plans.Plans[1].Limits.Materials.ShouldBe(ContentLimitDefaults.Plus.Materials);
    }

    [Fact]
    public async Task WithoutAProvider_NothingIsAskedFor()
    {
        (await Catalog().GetPlansAsync(TestContext.Current.CancellationToken)).CheckoutAvailable.ShouldBeFalse();

        _provider.Verify(x => x.GetPricesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
