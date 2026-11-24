using Application.Ai.Pricing;
using Domain.Utility.Ai;
using Domain.Utility.Subscription;
using Shouldly;

namespace UnitTests.Ai;

public class PricingValidatorTests
{
    [Fact]
    public void TheShippedConfig_IsProfitable_AndPrintsItsMargins()
    {
        var pricing = TestPricing.Shipped();

        TestContext.Current.TestOutputHelper?.WriteLine(PricingValidator.MarginTable(pricing));

        PricingValidator.Validate(pricing).ShouldBeEmpty();
    }

    [Fact]
    public void TheShippedConfig_MatchesTheCostModel()
    {
        var pricing = TestPricing.Shipped();

        // Plus at $5: net $3.69 after 25% VAT and Stripe; every quota used up costs $3.52
        Math.Round(pricing.NetRevenue(5, "USD"), 2).ShouldBe(3.69m);
        Math.Round(PricingValidator.WorstCost(pricing, SubscriptionTier.Plus), 2).ShouldBe(3.52m);
        Math.Round(PricingValidator.WorstCost(pricing, SubscriptionTier.Duo), 2).ShouldBe(7.03m);
        Math.Round(PricingValidator.WorstCost(pricing, SubscriptionTier.Free), 2).ShouldBe(0.30m);
        Math.Round(PricingValidator.WorstCost(pricing, SubscriptionTier.Free, trial: true), 2).ShouldBe(0.35m);
    }

    [Fact]
    public void MoreQuotaThanThePriceCovers_Fails()
    {
        var pricing = TestPricing.Shipped();
        pricing.Plans[SubscriptionTier.Plus].Quotas[AiFeature.PodcastGeneration] = 10;

        PricingValidator.Validate(pricing).ShouldContain(x => x.StartsWith("Plus"));
    }

    [Fact]
    public void ACheaperPlan_Fails()
    {
        var pricing = TestPricing.Shipped();
        pricing.Plans[SubscriptionTier.Duo].Prices[0].Amount = 8;

        PricingValidator.Validate(pricing).ShouldContain(x => x.StartsWith("Duo"));
    }

    [Fact]
    public void CreditsBelowTwiceTheCost_Fail()
    {
        var pricing = TestPricing.Shipped();
        pricing.Features[AiFeature.PodcastGeneration].Credits = 40;

        PricingValidator.Validate(pricing).ShouldContain(x => x.StartsWith("PodcastGeneration"));
    }

    [Fact]
    public void AGenerousFreePlan_Fails()
    {
        var pricing = TestPricing.Shipped();
        pricing.Plans[SubscriptionTier.Free].LifetimeTrial[AiFeature.PodcastGeneration] = 3;

        PricingValidator.Validate(pricing).ShouldContain(x => x.StartsWith("Free trial"));
    }

    [Theory]
    [InlineData(31, 0, 49)]
    [InlineData(5, 0, 7)]
    [InlineData(1, 0, 7)]
    public void Video_PaysPerStartedFiveMinutes(int minutes, int before, int credits)
    {
        var video = TestPricing.Shipped().Features[AiFeature.VideoTranscript];

        CreditMath.CreditsFor(video, minutes, before).ShouldBe(credits);
    }
}
