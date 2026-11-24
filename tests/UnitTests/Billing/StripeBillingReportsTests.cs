using Application.Billing;
using Domain.Utility.Subscription;
using Infrastructure.Billing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Billing;

public class StripeBillingReportsTests
{
    private static readonly DateTime Since = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.Parse("8c6f3d2a-1b4e-4f5a-9c7d-0e1f2a3b4c5d");

    private readonly FakeStripeHandler _handler = new();
    private readonly StripeBillingReports _sut;

    public StripeBillingReportsTests()
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
                [(BillingPlan.Duo, BillingInterval.Year)] = "price_duo_year",
            },
            StripeTaxMode.None,
            0,
            new Uri("https://www.studytracker.xyz/"));

        _sut = new StripeBillingReports(
            new StripeApi(factory.Object, settings, NullLogger<StripeApi>.Instance),
            new MemoryCache(new MemoryCacheOptions()),
            settings);
    }

    private static long Unix(DateTime time) => new DateTimeOffset(time).ToUnixTimeSeconds();

    [Fact]
    public async Task Payments_ArePaidInvoices_WithoutTheEmptyOnesOfATrial()
    {
        var paidAt = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
        _handler.Answers.Enqueue($$$"""
            {"data":[
              {"id":"in_1","amount_paid":1299,"currency":"eur","created":{{{Unix(paidAt.AddHours(-1))}}},"status_transitions":{"paid_at":{{{Unix(paidAt)}}}}},
              {"id":"in_2","amount_paid":0,"currency":"eur","created":{{{Unix(paidAt)}}}}
            ],"has_more":false}
            """);

        var payments = await _sut.GetPaymentsAsync(Since, TestContext.Current.CancellationToken);

        payments.ShouldHaveSingleItem().ShouldBe(new BillingPayment(paidAt, 1299, "eur"));
        var query = Uri.UnescapeDataString(_handler.Requests.Single().RequestUri!.Query);
        query.ShouldContain("status=paid");
        query.ShouldContain($"created[gte]={Unix(Since)}");
    }

    [Fact]
    public async Task Balance_LeavesOutPayoutsAndTransfers()
    {
        var at = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
        _handler.Answers.Enqueue($$$"""
            {"data":[
              {"id":"txn_1","type":"charge","net":1180,"currency":"eur","created":{{{Unix(at)}}}},
              {"id":"txn_2","type":"refund","net":-1299,"currency":"eur","created":{{{Unix(at)}}}},
              {"id":"txn_3","type":"payout","net":-5000,"currency":"eur","created":{{{Unix(at)}}}},
              {"id":"txn_4","type":"transfer","net":-100,"currency":"eur","created":{{{Unix(at)}}}}
            ],"has_more":false}
            """);

        var balance = await _sut.GetBalanceAsync(Since, TestContext.Current.CancellationToken);

        balance.Select(x => x.Net).ShouldBe([1180L, -1299L]);
    }

    [Fact]
    public async Task Renewals_AreThePlansThatWillBePaidAgain()
    {
        var periodEnd = new DateTime(2026, 12, 3, 0, 0, 0, DateTimeKind.Utc);
        string Subscription(string id, string status, string price, string interval, bool canceling = false, int quantity = 1) => $$$"""
            {"id":"{{{id}}}","status":"{{{status}}}","cancel_at_period_end":{{{(canceling ? "true" : "false")}}},"metadata":{"user_id":"{{{UserId}}}"},
             "items":{"data":[{"quantity":{{{quantity}}},"current_period_end":{{{Unix(periodEnd)}}},
               "price":{"id":"{{{price}}}","unit_amount":1000,"currency":"eur","recurring":{"interval":"{{{interval}}}"} } } ] } }
            """;

        _handler.Answers.Enqueue($$$"""
            {"data":[
              {{{Subscription("sub_1", "active", "price_plus_month", "month")}}},
              {{{Subscription("sub_2", "trialing", "price_duo_year", "year", quantity: 2)}}},
              {{{Subscription("sub_3", "active", "price_plus_month", "month", canceling: true)}}},
              {{{Subscription("sub_4", "canceled", "price_plus_month", "month")}}}
            ],"has_more":false}
            """);

        var renewals = await _sut.GetRenewalsAsync(TestContext.Current.CancellationToken);

        renewals.Count.ShouldBe(2);
        renewals[0].ShouldBe(new BillingRenewal(UserId, SubscriptionTier.Plus, BillingInterval.Month, 1000, "eur", periodEnd));
        renewals[1].ShouldBe(new BillingRenewal(UserId, SubscriptionTier.Duo, BillingInterval.Year, 2000, "eur", periodEnd));
    }

    [Fact]
    public async Task Lists_AreReadPageByPage_AndKeptForAWhile()
    {
        _handler.Answers.Enqueue("""{"data":[{"id":"txn_1","type":"charge","net":100,"currency":"eur","created":1790000000}],"has_more":true}""");
        _handler.Answers.Enqueue("""{"data":[{"id":"txn_2","type":"charge","net":200,"currency":"eur","created":1790000000}],"has_more":false}""");

        (await _sut.GetBalanceAsync(Since, TestContext.Current.CancellationToken)).Count.ShouldBe(2);
        (await _sut.GetBalanceAsync(Since, TestContext.Current.CancellationToken)).Count.ShouldBe(2);

        _handler.Requests.Count.ShouldBe(2);
        _handler.Requests[1].RequestUri!.Query.ShouldContain("starting_after=txn_1");
    }
}
