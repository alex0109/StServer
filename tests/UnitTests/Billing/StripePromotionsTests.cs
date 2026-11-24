using System.Net;
using Application.Billing;
using Application.Exceptions;
using Infrastructure.Billing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Billing;

public class StripePromotionsTests
{
    private static readonly Guid AdminId = Guid.Parse("8c6f3d2a-1b4e-4f5a-9c7d-0e1f2a3b4c5d");

    private readonly FakeStripeHandler _handler = new();
    private readonly StripePromotions _sut;

    public StripePromotionsTests()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(StripeApi.HttpClientName))
            .Returns(() => new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.stripe.com/") });

        var settings = new StripeSettings(
            "sk_test_key",
            "whsec_test_only",
            new Dictionary<(BillingPlan, BillingInterval), string>
            {
                [(BillingPlan.Plus, BillingInterval.Month)] = "price_plus_month",
                [(BillingPlan.Plus, BillingInterval.Year)] = "price_plus_year",
                [(BillingPlan.Duo, BillingInterval.Month)] = "price_duo_month",
            },
            StripeTaxMode.None,
            0,
            new Uri("https://www.studytracker.xyz/"));

        _sut = new StripePromotions(new StripeApi(factory.Object, settings, NullLogger<StripeApi>.Instance), settings, NullLogger<StripePromotions>.Instance);
    }

    private static PromoBatch Batch(PromoKind kind, BillingPlan? plan, int percent = 0, PromoDuration duration = PromoDuration.Months, int? months = 3, DateTime? expires = null) =>
        new("Deichman Bjørvika", kind, plan, percent, duration, months, 50, false, expires, AdminId);

    private static string Price(string product, long amount) => $$"""
        {"id":"price","product":"{{product}}","unit_amount":{{amount}},"currency":"eur","currency_options":{"eur":{"unit_amount":{{amount}} },"nok":{"unit_amount":11900} } }
        """;

    [Fact]
    public async Task FreeMonths_TakeTheMonthlyPriceOffEachPayment_OnlyForThatPlan()
    {
        var expires = new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        _handler.Respond = path => path switch
        {
            "/v1/prices/price_plus_month" => Price("prod_plus", 999),
            "/v1/prices/price_plus_year" => Price("prod_plus", 9900),
            "/v1/coupons" => """{"id":"co_1"}""",
            _ => "{}",
        };

        (await _sut.CreateDiscountAsync(Batch(PromoKind.FreeMonths, BillingPlan.Plus, expires: expires), TestContext.Current.CancellationToken)).ShouldBe("co_1");

        var coupon = _handler.Forms.Single();
        coupon["amount_off"].ShouldBe("999");
        coupon["currency"].ShouldBe("eur");
        coupon["currency_options[nok][amount_off]"].ShouldBe("11900");
        coupon.ContainsKey("currency_options[eur][amount_off]").ShouldBeFalse();
        coupon["duration"].ShouldBe("repeating");
        coupon["duration_in_months"].ShouldBe("3");
        coupon["applies_to[products][0]"].ShouldBe("prod_plus");
        coupon.ContainsKey("applies_to[products][1]").ShouldBeFalse();
        coupon["max_redemptions"].ShouldBe("50");
        coupon["redeem_by"].ShouldBe(new DateTimeOffset(expires).ToUnixTimeSeconds().ToString());
        coupon["metadata[source]"].ShouldBe("studytracker-admin");
        coupon["metadata[kind]"].ShouldBe("FreeMonths");
        coupon.ContainsKey("percent_off").ShouldBeFalse();
    }

    [Fact]
    public async Task PercentOff_OnBothPlans_HasNoProductLimit()
    {
        _handler.Answers.Enqueue("""{"id":"co_2"}""");

        await _sut.CreateDiscountAsync(Batch(PromoKind.PercentOff, null, percent: 20, duration: PromoDuration.Once, months: null), TestContext.Current.CancellationToken);

        var coupon = _handler.Forms.Single();
        coupon["percent_off"].ShouldBe("20");
        coupon["duration"].ShouldBe("once");
        coupon["metadata[plan]"].ShouldBe("all");
        coupon.Keys.ShouldNotContain(key => key.StartsWith("applies_to"));
        coupon.ContainsKey("duration_in_months").ShouldBeFalse();
    }

    [Fact]
    public async Task Codes_PointAtTheCoupon_AndARefusedOneIsLeftOut()
    {
        _handler.StatusFor = (_, form) => form.GetValueOrDefault("code") == "TAKEN" ? HttpStatusCode.BadRequest : null;

        var created = await _sut.AddCodesAsync("co_1", ["ONE", "TAKEN", "TWO"], new PromoCodeLimits(3, NewCustomersOnly: true), TestContext.Current.CancellationToken);

        created.ShouldBe(["ONE", "TWO"]);
        var form = _handler.Forms.Single(x => x["code"] == "ONE");
        form["promotion[type]"].ShouldBe("coupon");
        form["promotion[coupon]"].ShouldBe("co_1");
        form["max_redemptions"].ShouldBe("3");
        form["restrictions[first_time_transaction]"].ShouldBe("true");
    }

    [Fact]
    public async Task List_ShowsOnlyBatchesMadeHere_WithTheirCodes()
    {
        _handler.Respond = path => path switch
        {
            "/v1/coupons" => """
                {"data":[
                  {"id":"co_1","name":"Deichman","amount_off":999,"currency":"eur","duration":"repeating","duration_in_months":3,"valid":true,"created":1790000000,
                   "metadata":{"source":"studytracker-admin","kind":"FreeMonths","plan":"Plus","new_customers_only":"true"}},
                  {"id":"co_hand","name":"Made in the dashboard","percent_off":10,"duration":"once","valid":true,"created":1790000100,"metadata":{}},
                  {"id":"co_empty","name":"Failed","percent_off":10,"duration":"once","valid":true,"created":1790000200,"metadata":{"source":"studytracker-admin","kind":"PercentOff","plan":"all"}}
                ],"has_more":false}
                """,
            "/v1/promotion_codes" => """
                {"data":[
                  {"id":"promo_2","code":"DEICH-BBBBBB","active":false,"times_redeemed":1,"max_redemptions":1,"promotion":{"type":"coupon","coupon":"co_1"}},
                  {"id":"promo_1","code":"DEICH-AAAAAA","active":true,"times_redeemed":0,"max_redemptions":1,"promotion":{"type":"coupon","coupon":"co_1"}},
                  {"id":"promo_3","code":"HAND","active":true,"times_redeemed":0,"promotion":{"type":"coupon","coupon":"co_hand"}}
                ],"has_more":false}
                """,
            _ => "{}",
        };

        var batch = (await _sut.ListAsync(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        batch.Id.ShouldBe("co_1");
        batch.Kind.ShouldBe(PromoKind.FreeMonths);
        batch.Plan.ShouldBe(BillingPlan.Plus);
        batch.Months.ShouldBe(3);
        batch.AmountOff.ShouldBe(999);
        batch.NewCustomersOnly.ShouldBeTrue();
        batch.Codes.Select(x => x.Code).ShouldBe(["DEICH-AAAAAA", "DEICH-BBBBBB"]);
        batch.Codes[1].TimesRedeemed.ShouldBe(1);
    }

    [Fact]
    public async Task Deactivate_SwitchesOffTheActiveCodes_OfABatchMadeHere()
    {
        _handler.Respond = path => path switch
        {
            "/v1/coupons/co_1" => """{"id":"co_1","metadata":{"source":"studytracker-admin"}}""",
            "/v1/coupons/co_hand" => """{"id":"co_hand","metadata":{}}""",
            "/v1/promotion_codes" => """{"data":[{"id":"promo_1","active":true},{"id":"promo_2","active":true}],"has_more":false}""",
            _ => "{}",
        };

        (await _sut.DeactivateAsync("co_1", TestContext.Current.CancellationToken)).ShouldBe(2);
        _handler.Requests.Where(x => x.Method == HttpMethod.Post).Select(x => x.RequestUri!.AbsolutePath).Order()
            .ShouldBe(["/v1/promotion_codes/promo_1", "/v1/promotion_codes/promo_2"]);
        _handler.Forms.ShouldAllBe(form => form["active"] == "false");

        await Should.ThrowAsync<BadRequestException>(() => _sut.DeactivateAsync("co_hand", TestContext.Current.CancellationToken));
    }
}
