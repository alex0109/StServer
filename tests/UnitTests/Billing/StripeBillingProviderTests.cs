using System.Net;
using System.Security.Cryptography;
using System.Text;
using Application.Billing;
using Application.Exceptions;
using Domain.Utility.Subscription;
using Infrastructure.Billing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Billing;

public class StripeBillingProviderTests
{
    private const string Secret = "whsec_test_only";
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.Parse("8c6f3d2a-1b4e-4f5a-9c7d-0e1f2a3b4c5d");

    private readonly FakeHandler _handler = new();
    private readonly Mock<IHttpClientFactory> _factory = new();

    public StripeBillingProviderTests()
    {
        _factory.Setup(x => x.CreateClient(StripeBillingProvider.HttpClientName))
            .Returns(() => new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.stripe.com/") });
    }

    private static StripeSettings Settings(StripeTaxMode tax = StripeTaxMode.None, int trialDays = 0) => new(
        "sk_test_key",
        Secret,
        new Dictionary<(BillingPlan, BillingInterval), string>
        {
            [(BillingPlan.Plus, BillingInterval.Month)] = "price_plus_month",
            [(BillingPlan.Plus, BillingInterval.Year)] = "price_plus_year",
            [(BillingPlan.Pro, BillingInterval.Month)] = "price_pro_month",
        },
        tax,
        trialDays,
        new Uri("https://www.studytracker.xyz/"));

    private StripeBillingProvider Provider(StripeSettings? settings = null) => new(
        _factory.Object,
        new MemoryCache(new MemoryCacheOptions()),
        settings ?? Settings(),
        NullLogger<StripeBillingProvider>.Instance);

    private static string Signature(string secret, long timestamp, byte[] body)
    {
        byte[] payload = [.. Encoding.ASCII.GetBytes($"{timestamp}."), .. body];
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload)).ToLowerInvariant();
    }

    private static long Unix(DateTime time) => new DateTimeOffset(time).ToUnixTimeSeconds();

    private static byte[] SubscriptionEvent(
        string type = "customer.subscription.updated",
        string status = "active",
        string price = "price_plus_month",
        bool cancelAtPeriodEnd = false,
        DateTime? cancelAt = null) =>
        Encoding.UTF8.GetBytes($$"""
        {
          "id": "evt_1",
          "object": "event",
          "type": "{{type}}",
          "created": {{Unix(Now)}},
          "data": {
            "object": {
              "id": "sub_1",
              "object": "subscription",
              "customer": "cus_1",
              "status": "{{status}}",
              "cancel_at_period_end": {{(cancelAtPeriodEnd ? "true" : "false")}},
              "cancel_at": {{(cancelAt is { } at ? Unix(at).ToString() : "null")}},
              "trial_end": null,
              "metadata": { "user_id": "{{UserId}}" },
              "items": {
                "object": "list",
                "data": [
                  {
                    "id": "si_1",
                    "current_period_start": {{Unix(Now.AddDays(-1))}},
                    "current_period_end": {{Unix(Now.AddDays(29))}},
                    "price": { "id": "{{price}}", "recurring": { "interval": "month" } }
                  }
                ]
              }
            }
          }
        }
        """);

    [Fact]
    public void Webhook_SignedByStripe_IsAcceptedForFiveMinutes()
    {
        var body = SubscriptionEvent();
        var signedAt = Unix(Now);
        var header = $"t={signedAt},v1={Signature(Secret, signedAt, body)},v0=ignored";
        var provider = Provider();

        provider.VerifyWebhook(header, body, Now.AddMinutes(4)).ShouldBeTrue();
        // A captured delivery can't be replayed later
        provider.VerifyWebhook(header, body, Now.AddMinutes(6)).ShouldBeFalse();
        // Signed for another body
        provider.VerifyWebhook(header, [.. body, (byte)' '], Now).ShouldBeFalse();
    }

    [Fact]
    public void Webhook_WhileTheSecretRolls_EitherSignatureCounts()
    {
        var body = SubscriptionEvent();
        var signedAt = Unix(Now);
        var header = $"t={signedAt},v1={Signature("whsec_old", signedAt, body)},v1={Signature(Secret, signedAt, body)}";

        Provider().VerifyWebhook(header, body, Now).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1=abc")]
    [InlineData("t=notanumber,v1=abc")]
    [InlineData("t=1793448000")]
    public void Webhook_WithoutAProperSignature_IsRefused(string? header)
    {
        Provider().VerifyWebhook(header, SubscriptionEvent(), Now).ShouldBeFalse();
    }

    [Fact]
    public void SubscriptionEvent_IsReadInTheAppsWords()
    {
        var parsed = Provider().ParseEvent(SubscriptionEvent(type: "customer.subscription.created")).ShouldNotBeNull();

        parsed.EventId.ShouldBe("evt_1");
        parsed.Type.ShouldBe("customer.subscription.created");
        parsed.OccurredAt.ShouldBe(Now);
        parsed.UserId.ShouldBe(UserId);
        parsed.CustomerId.ShouldBe("cus_1");
        parsed.SubscriptionId.ShouldBe("sub_1");
        parsed.Tier.ShouldBe(SubscriptionTier.Plus);
        parsed.Status.ShouldBe(SubscriptionStatus.Active);
        // Since API 2025-03-31 the period comes from the subscription item
        parsed.PeriodStart.ShouldBe(Now.AddDays(-1));
        parsed.PeriodEnd.ShouldBe(Now.AddDays(29));
        parsed.CancelAtPeriodEnd.ShouldBeFalse();
    }

    [Theory]
    [InlineData("trialing", SubscriptionStatus.Trialing)]
    [InlineData("past_due", SubscriptionStatus.PastDue)]
    // The first payment isn't through yet: nothing is given before it is
    [InlineData("incomplete", SubscriptionStatus.Expired)]
    [InlineData("incomplete_expired", SubscriptionStatus.Expired)]
    [InlineData("unpaid", SubscriptionStatus.Expired)]
    [InlineData("paused", SubscriptionStatus.Expired)]
    [InlineData("canceled", SubscriptionStatus.Expired)]
    public void Statuses_GiveAccessOnlyWhilePaidFor(string status, SubscriptionStatus expected)
    {
        Provider().ParseEvent(SubscriptionEvent(status: status))!.Status.ShouldBe(expected);
    }

    [Fact]
    public void CancellingAtTheEnd_OrOnADate_MeansItWontRenew()
    {
        var provider = Provider();

        provider.ParseEvent(SubscriptionEvent(cancelAtPeriodEnd: true))!.CancelAtPeriodEnd.ShouldBeTrue();
        provider.ParseEvent(SubscriptionEvent(cancelAt: Now.AddDays(29)))!.CancelAtPeriodEnd.ShouldBeTrue();
        // Still paid for until then
        provider.ParseEvent(SubscriptionEvent(cancelAt: Now.AddDays(29)))!.Status.ShouldBe(SubscriptionStatus.Active);
        provider.ParseEvent(SubscriptionEvent(price: "price_pro_month"))!.Tier.ShouldBe(SubscriptionTier.Pro);
    }

    [Fact]
    public void OtherEvents_AndUnknownPrices()
    {
        var provider = Provider();

        provider.ParseEvent("""{"id":"evt_2","type":"checkout.session.completed","created":1,"data":{"object":{"object":"checkout.session"}}}"""u8.ToArray())
            .ShouldBeNull();
        provider.ParseEvent("not json"u8.ToArray()).ShouldBeNull();

        // A price made by hand in the dashboard gives no tier on its own
        provider.ParseEvent(SubscriptionEvent(price: "price_someone_else"))!.Tier.ShouldBeNull();
    }

    [Fact]
    public async Task Checkout_CarriesTheUserIdTheServerKnows()
    {
        _handler.Answers.Enqueue("""{"url":"https://checkout.stripe.com/c/pay/cs_test_1"}""");

        var url = await Provider(Settings(trialDays: 7)).CreateCheckoutUrlAsync(
            new BillingCheckout(UserId, "reader@example.com", BillingPlan.Plus, BillingInterval.Year, null, "nb"),
            TestContext.Current.CancellationToken);

        url.ShouldBe("https://checkout.stripe.com/c/pay/cs_test_1");
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.ShouldBe("/v1/checkout/sessions");
        request.Headers.Authorization!.Parameter.ShouldBe("sk_test_key");
        request.Headers.GetValues("Stripe-Version").ShouldHaveSingleItem().ShouldBe(StripeBillingProvider.ApiVersion);

        var form = _handler.Forms.Single();
        form["mode"].ShouldBe("subscription");
        form["line_items[0][price]"].ShouldBe("price_plus_year");
        form["client_reference_id"].ShouldBe(UserId.ToString());
        form["subscription_data[metadata][user_id]"].ShouldBe(UserId.ToString());
        form["success_url"].ShouldBe("https://www.studytracker.xyz/limits?checkout=success");
        form["cancel_url"].ShouldBe("https://www.studytracker.xyz/limits?checkout=cancelled");
        form["customer_email"].ShouldBe("reader@example.com");
        form["locale"].ShouldBe("nb");
        // First plan: the trial is offered
        form["subscription_data[trial_period_days]"].ShouldBe("7");
        form.ShouldNotContainKey("automatic_tax[enabled]");
        form.ShouldNotContainKey("managed_payments[enabled]");
    }

    [Fact]
    public async Task Checkout_ForAFormerCustomer_ReusesIt_WithoutATrial()
    {
        _handler.Answers.Enqueue("""{"url":"https://checkout.stripe.com/c/pay/cs_test_2"}""");

        await Provider(Settings(StripeTaxMode.Automatic, trialDays: 7)).CreateCheckoutUrlAsync(
            new BillingCheckout(UserId, "reader@example.com", BillingPlan.Pro, BillingInterval.Month, "cus_1", "uk"),
            TestContext.Current.CancellationToken);

        var form = _handler.Forms.Single();
        form["customer"].ShouldBe("cus_1");
        form.ShouldNotContainKey("customer_email");
        form.ShouldNotContainKey("subscription_data[trial_period_days]");
        form["locale"].ShouldBe("auto");
        form["automatic_tax[enabled]"].ShouldBe("true");
        form["billing_address_collection"].ShouldBe("required");
        form["customer_update[address]"].ShouldBe("auto");
    }

    [Fact]
    public async Task Checkout_WithManagedPayments_LeavesTaxToStripe()
    {
        _handler.Answers.Enqueue("""{"url":"https://checkout.stripe.com/c/pay/cs_test_3"}""");

        await Provider(Settings(StripeTaxMode.Managed)).CreateCheckoutUrlAsync(
            new BillingCheckout(UserId, null, BillingPlan.Plus, BillingInterval.Month, null, "en-GB"),
            TestContext.Current.CancellationToken);

        var form = _handler.Forms.Single();
        form["managed_payments[enabled]"].ShouldBe("true");
        form.ShouldNotContainKey("automatic_tax[enabled]");
        form.ShouldNotContainKey("tax_id_collection[enabled]");
        form.ShouldNotContainKey("customer_email");
    }

    [Fact]
    public async Task Checkout_ForAPlanWithoutAPrice_IsUnavailable()
    {
        await Should.ThrowAsync<BillingUnavailableException>(() => Provider().CreateCheckoutUrlAsync(
            new BillingCheckout(UserId, null, BillingPlan.Pro, BillingInterval.Year, null, null),
            TestContext.Current.CancellationToken));

        _handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task StripeErrors_BecomeUnavailable()
    {
        _handler.Statuses.Enqueue(HttpStatusCode.BadRequest);
        _handler.Answers.Enqueue("""{"error":{"code":"resource_missing","message":"No such price"}}""");

        await Should.ThrowAsync<BillingUnavailableException>(() => Provider().CreateCheckoutUrlAsync(
            new BillingCheckout(UserId, null, BillingPlan.Plus, BillingInterval.Month, null, null),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Portal_ReturnsToTheLimitsPage()
    {
        _handler.Answers.Enqueue("""{"url":"https://billing.stripe.com/p/session/test_1"}""");

        (await Provider().CreatePortalUrlAsync("cus_1", "uk", TestContext.Current.CancellationToken)).ShouldBe("https://billing.stripe.com/p/session/test_1");

        _handler.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/v1/billing_portal/sessions");
        var form = _handler.Forms.Single();
        form["customer"].ShouldBe("cus_1");
        form["return_url"].ShouldBe("https://www.studytracker.xyz/limits");
    }

    [Fact]
    public async Task DeleteCustomer_ThatIsGoneAlready_IsFine()
    {
        _handler.Statuses.Enqueue(HttpStatusCode.NotFound);
        _handler.Answers.Enqueue("""{"error":{"code":"resource_missing"}}""");

        await Provider().DeleteCustomerAsync("cus_1", TestContext.Current.CancellationToken);

        var request = _handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.RequestUri!.AbsolutePath.ShouldBe("/v1/customers/cus_1");

        // Anything else is retried by the job
        _handler.Statuses.Enqueue(HttpStatusCode.InternalServerError);
        await Should.ThrowAsync<BillingUnavailableException>(() => Provider().DeleteCustomerAsync("cus_1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Prices_AreReadOnce_AndWrongOnesSkipped()
    {
        _handler.Respond = path => path switch
        {
            "/v1/prices/price_plus_month" => """{"id":"price_plus_month","active":true,"currency":"nok","unit_amount":9900,"recurring":{"interval":"month"}}""",
            // Configured as yearly, but it is a monthly price
            "/v1/prices/price_plus_year" => """{"id":"price_plus_year","active":true,"currency":"nok","unit_amount":99000,"recurring":{"interval":"month"}}""",
            "/v1/prices/price_pro_month" => """{"id":"price_pro_month","active":false,"currency":"nok","unit_amount":19900,"recurring":{"interval":"month"}}""",
            _ => "{}",
        };
        var provider = Provider();

        var prices = await provider.GetPricesAsync(TestContext.Current.CancellationToken);

        prices.ShouldHaveSingleItem().ShouldBe(new BillingPrice(BillingPlan.Plus, BillingInterval.Month, 9900, "nok"));
        (await provider.GetPricesAsync(TestContext.Current.CancellationToken)).ShouldBeSameAs(prices);
        _handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public void WithoutKeys_ItIsOff()
    {
        Provider(Settings() with { SecretKey = null }).IsConfigured.ShouldBeFalse();
        Provider(Settings() with { WebhookSecret = null }).IsConfigured.ShouldBeFalse();
        Provider(Settings() with { SiteUrl = null }).IsConfigured.ShouldBeFalse();
        Provider(Settings() with { Prices = new Dictionary<(BillingPlan, BillingInterval), string>() }).IsConfigured.ShouldBeFalse();
        Provider().IsConfigured.ShouldBeTrue();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public Queue<string> Answers { get; } = new();
        public Queue<HttpStatusCode> Statuses { get; } = new();
        public Func<string, string>? Respond { get; set; }
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<Dictionary<string, string>> Forms { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null)
            {
                var text = await request.Content.ReadAsStringAsync(cancellationToken);
                Forms.Add(text.Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(pair => pair.Split('=', 2))
                    .ToDictionary(pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')), pair => Uri.UnescapeDataString(pair[1].Replace('+', ' '))));
            }

            var body = Respond?.Invoke(request.RequestUri!.AbsolutePath) ?? (Answers.Count > 0 ? Answers.Dequeue() : "{}");
            var status = Statuses.Count > 0 ? Statuses.Dequeue() : HttpStatusCode.OK;
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}

[Collection("Environment")]
public class StripeSettingsTests : IDisposable
{
    private static readonly string[] Variables =
    [
        "STRIPE_SECRET_KEY", "STRIPE_WEBHOOK_SECRET", "STRIPE_TAX_MODE", "STRIPE_TRIAL_DAYS",
        "STRIPE_PRICE_PLUS_MONTHLY", "STRIPE_PRICE_PLUS_YEARLY", "STRIPE_PRICE_PRO_MONTHLY", "STRIPE_PRICE_PRO_YEARLY",
    ];

    public StripeSettingsTests() => Dispose();

    public void Dispose()
    {
        foreach (var name in Variables) Environment.SetEnvironmentVariable(name, null);
    }

    [Fact]
    public void NothingSet_IsOff()
    {
        StripeSettings.FromEnvironment("https://www.studytracker.xyz").IsComplete.ShouldBeFalse();
    }

    [Fact]
    public void ReadsKeysPricesAndOptions()
    {
        Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY", "rk_live_restricted");
        Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", "whsec_abc");
        Environment.SetEnvironmentVariable("STRIPE_PRICE_PLUS_MONTHLY", "price_1");
        // Not a price id: ignored
        Environment.SetEnvironmentVariable("STRIPE_PRICE_PRO_MONTHLY", "prod_1");
        Environment.SetEnvironmentVariable("STRIPE_TAX_MODE", "Managed");
        Environment.SetEnvironmentVariable("STRIPE_TRIAL_DAYS", "400");

        var settings = StripeSettings.FromEnvironment("https://www.studytracker.xyz");

        settings.IsComplete.ShouldBeTrue();
        settings.SecretKey.ShouldBe("rk_live_restricted");
        settings.Prices.ShouldHaveSingleItem().Value.ShouldBe("price_1");
        settings.TaxMode.ShouldBe(StripeTaxMode.Managed);
        settings.TrialDays.ShouldBe(90);
        settings.SiteUrl.ShouldBe(new Uri("https://www.studytracker.xyz/"));
    }

    [Fact]
    public void ASiteWithoutHttps_IsRefused_ExceptOnThisMachine()
    {
        StripeSettings.FromEnvironment("http://studytracker.xyz").SiteUrl.ShouldBeNull();
        StripeSettings.FromEnvironment("http://localhost:3000").SiteUrl.ShouldBe(new Uri("http://localhost:3000/"));
    }
}
