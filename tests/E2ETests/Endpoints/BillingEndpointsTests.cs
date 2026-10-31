using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace E2ETests.Endpoints;

// Stripe isn't configured in tests: everything must answer as "payments are off"
public class BillingEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _noAuthClient;

    public BillingEndpointsTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _noAuthClient = factory.CreateClientWithoutAuth();
    }

    [Fact]
    public async Task Plans_AreOpenToGuests_WithLimitsButNoPrices()
    {
        var response = await _noAuthClient.GetAsync("/api/billing/plans", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = json.RootElement;

        root.GetProperty("checkoutAvailable").GetBoolean().ShouldBeFalse();
        var plans = root.GetProperty("plans").EnumerateArray().ToList();
        plans.Select(x => x.GetProperty("tier").GetString()).ShouldBe(["Free", "Plus", "Pro"]);
        plans.ShouldAllBe(x => x.GetProperty("prices").GetArrayLength() == 0);

        plans[1].GetProperty("limits").GetProperty("materials").GetInt32().ShouldBe(300);
        // Pro has no limit on materials
        plans[2].GetProperty("limits").GetProperty("materials").ValueKind.ShouldBe(JsonValueKind.Null);
        plans[1].GetProperty("aiPerMonth").GetProperty("QuestionGeneration").GetInt32().ShouldBe(100);
    }

    [Fact]
    public async Task Status_SaysPaymentsAreOff()
    {
        var response = await _client.GetAsync("/api/billing/status", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("tier").GetString().ShouldBe("Free");
        json.RootElement.GetProperty("checkoutAvailable").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("canManage").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Checkout_Portal_AndWebhook_WithoutStripe()
    {
        var checkout = await _client.PostAsJsonAsync(
            "/api/billing/checkout",
            new { plan = "Plus", interval = "Year", language = "uk" },
            TestContext.Current.CancellationToken);
        checkout.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        var portal = await _client.PostAsJsonAsync(
            "/api/billing/portal",
            new { language = "uk" },
            TestContext.Current.CancellationToken);
        portal.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // An unconfigured provider has no webhook at all
        var webhook = await _noAuthClient.PostAsync(
            "/api/billing/webhook/stripe",
            new StringContent("{}"),
            TestContext.Current.CancellationToken);
        webhook.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
