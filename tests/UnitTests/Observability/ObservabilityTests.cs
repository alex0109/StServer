using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.DTOs.Admin;
using Application.Interfaces;
using Infrastructure.Observability;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace UnitTests.Observability;

public class MetricsTokenTests
{
    private static string Basic(string user, string password) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));

    [Fact]
    public void BearerOrBasicPassword_OpenTheEndpoint()
    {
        MetricsToken.Allowed("Bearer s3cret", "s3cret").ShouldBeTrue();
        MetricsToken.Allowed(Basic("123456", "s3cret"), "s3cret").ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bearer")]
    [InlineData("Bearer wrong")]
    [InlineData("Basic not-base64!")]
    [InlineData("Token s3cret")]
    public void AnythingElse_IsRefused(string header)
    {
        MetricsToken.Allowed(header, "s3cret").ShouldBeFalse();
    }
}

// The clients read their settings from environment variables, so these tests run one at a time
[Collection("Environment")]
public class ObservabilityClientTests : IDisposable
{
    private static readonly string[] Variables =
        ["PROMETHEUS_URL", "PROMETHEUS_USER", "PROMETHEUS_TOKEN", "SENTRY_API_URL", "SENTRY_API_TOKEN", "SENTRY_ORG", "GA_PROPERTY_ID", "GA_CREDENTIALS_JSON"];

    private readonly FakeHandler _handler = new();
    private readonly Mock<IHttpClientFactory> _factory = new();

    public ObservabilityClientTests()
    {
        _factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(_handler, disposeHandler: false));
    }

    public void Dispose()
    {
        foreach (var name in Variables) Environment.SetEnvironmentVariable(name, null);
    }

    [Fact]
    public async Task Prometheus_ReadsSeries_AndSignsInWithBasicAuth()
    {
        Environment.SetEnvironmentVariable("PROMETHEUS_URL", "https://prometheus.example/api/prom");
        Environment.SetEnvironmentVariable("PROMETHEUS_USER", "123456");
        Environment.SetEnvironmentVariable("PROMETHEUS_TOKEN", "glc_token");
        _handler.Answer = """
            {"status":"success","data":{"resultType":"matrix","result":[
              {"metric":{"endpoint":"api/review/today"},"values":[[1793318400,"12.5"],[1793319300,"NaN"]]}]}}
            """;

        var series = await new PrometheusApi(_factory.Object).QueryRangeAsync("sum(rate(http_requests_received_total[5m]))", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

        var only = series.ShouldHaveSingleItem();
        only.Labels["endpoint"].ShouldBe("api/review/today");
        only.Points.Select(x => x.Value).ShouldBe([12.5, null]);
        var request = _handler.Requests.Single();
        request.RequestUri!.AbsolutePath.ShouldBe("/api/prom/api/v1/query_range");
        request.Headers.Authorization!.Scheme.ShouldBe("Basic");
    }

    [Fact]
    public void Prometheus_KeysNeverGoOverPlainHttp()
    {
        Environment.SetEnvironmentVariable("PROMETHEUS_URL", "http://prometheus.example");
        new PrometheusApi(_factory.Object).IsConfigured.ShouldBeFalse();

        Environment.SetEnvironmentVariable("PROMETHEUS_URL", "http://localhost:9090");
        new PrometheusApi(_factory.Object).IsConfigured.ShouldBeTrue();
    }

    [Fact]
    public async Task Sentry_ReadsIssues_AndAcceptedErrorsPerHour()
    {
        Environment.SetEnvironmentVariable("SENTRY_API_URL", "https://de.sentry.io");
        Environment.SetEnvironmentVariable("SENTRY_API_TOKEN", "sntrys_token");
        Environment.SetEnvironmentVariable("SENTRY_ORG", "reybey-org");
        _handler.Answers.Enqueue("""
            [{"id":"42","shortId":"JAVASCRIPT-NEXTJS-7","title":"TypeError: x is undefined","culprit":"app/today",
              "level":"error","count":"17","userCount":3,"firstSeen":"2026-10-30T10:00:00Z","lastSeen":"2026-10-31T09:00:00Z",
              "permalink":"https://reybey-org.sentry.io/issues/42/","project":{"slug":"javascript-nextjs"},
              "stats":{"24h":[[1793318400,2],[1793322000,5]]}}]
            """);
        _handler.Answers.Enqueue("""
            {"intervals":["2026-10-31T08:00:00Z","2026-10-31T09:00:00Z"],
             "groups":[{"by":{"outcome":"accepted"},"series":{"sum(quantity)":[4,6]}},
                       {"by":{"outcome":"rate_limited"},"series":{"sum(quantity)":[100,100]}}]}
            """);

        var overview = await new SentryApi(_factory.Object).GetOverviewAsync(TestContext.Current.CancellationToken);

        var issue = overview.Issues.ShouldNotBeNull().ShouldHaveSingleItem();
        issue.Count.ShouldBe(17);
        issue.Trend.ShouldBe([2, 5]);
        overview.ErrorsLast24h.ShouldBe(10);
        _handler.Requests[0].RequestUri!.Host.ShouldBe("de.sentry.io");
    }

    [Fact]
    public void Sentry_OrgMustBeASlug()
    {
        Environment.SetEnvironmentVariable("SENTRY_API_TOKEN", "sntrys_token");
        Environment.SetEnvironmentVariable("SENTRY_ORG", "../admin");

        new SentryApi(_factory.Object).IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public async Task GoogleAnalytics_SignsInWithTheServiceAccount_AndReadsReports()
    {
        using var rsa = RSA.Create(2048);
        var credentials = JsonSerializer.Serialize(new
        {
            client_email = "panel@studytracker.iam.gserviceaccount.com",
            private_key = rsa.ExportPkcs8PrivateKeyPem(),
            token_uri = "https://oauth2.googleapis.com/token",
        });
        Environment.SetEnvironmentVariable("GA_PROPERTY_ID", "123456789");
        Environment.SetEnvironmentVariable("GA_CREDENTIALS_JSON", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));

        _handler.Answers.Enqueue("""{"access_token":"ya29.token","expires_in":3600}""");
        _handler.Answers.Enqueue("""
            {"reports":[
              {"rows":[{"dimensionValues":[{"value":"20261030"}],"metricValues":[{"value":"5"},{"value":"7"},{"value":"30"},{"value":"2"},{"value":"0.61"}]}],
               "totals":[{"metricValues":[{"value":"9"},{"value":"11"},{"value":"44"},{"value":"4"},{"value":"0.6"}]}]},
              {"rows":[{"dimensionValues":[{"value":"/"}],"metricValues":[{"value":"20"}]}]},
              {"rows":[{"dimensionValues":[{"value":"Norway"}],"metricValues":[{"value":"6"}]}]},
              {"rows":[{"dimensionValues":[{"value":"mobile"}],"metricValues":[{"value":"5"}]}]},
              {"rows":[{"dimensionValues":[{"value":"Organic Search"}],"metricValues":[{"value":"8"}]}]}]}
            """);
        _handler.Answers.Enqueue("""{"rows":[{"metricValues":[{"value":"3"}]}]}""");

        var overview = await new GoogleAnalyticsApi(_factory.Object).GetOverviewAsync(TestContext.Current.CancellationToken);

        overview.ActiveNow.ShouldBe(3);
        overview.Totals!.Users.ShouldBe(9);
        overview.Totals.EngagementRate.ShouldBe(60);
        overview.Days!.Single().Date.ShouldBe("2026-10-30");
        overview.Countries!.Single().Name.ShouldBe("Norway");

        // The token request carries a JWT that the account's public key verifies
        var form = await _handler.Bodies[0];
        var assertion = Uri.UnescapeDataString(form.Split("assertion=")[1]);
        var parts = assertion.Split('.');
        var signature = Convert.FromBase64String(parts[2].Replace('-', '+').Replace('_', '/').PadRight((parts[2].Length + 3) / 4 * 4, '='));
        rsa.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .ShouldBeTrue();
        _handler.Requests[1].Headers.Authorization!.Parameter.ShouldBe("ya29.token");
    }

    [Fact]
    public void GoogleAnalytics_BrokenCredentials_MeanNotConfigured()
    {
        Environment.SetEnvironmentVariable("GA_PROPERTY_ID", "123456789");
        Environment.SetEnvironmentVariable("GA_CREDENTIALS_JSON", "not a key");

        new GoogleAnalyticsApi(_factory.Object).IsConfigured.ShouldBeFalse();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public string? Answer { get; set; }
        public Queue<string> Answers { get; } = new();
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<Task<string>> Bodies { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult(""));
            var body = Answers.Count > 0 ? Answers.Dequeue() : Answer ?? "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}

public class ObservabilityServiceTests
{
    private readonly Mock<IPrometheusApi> _prometheus = new();
    private readonly Mock<IGrafanaApi> _grafana = new();
    private readonly Mock<ISentryApi> _sentry = new();
    private readonly Mock<IGoogleAnalyticsApi> _analytics = new();

    private ObservabilityService Service() => new(
        _prometheus.Object, _grafana.Object, _sentry.Object, _analytics.Object,
        new MemoryCache(new MemoryCacheOptions()), TimeProvider.System, NullLogger<ObservabilityService>.Instance);

    [Fact]
    public async Task NothingConfigured_NothingIsCalled()
    {
        var monitoring = await Service().GetMonitoringAsync(TestContext.Current.CancellationToken);

        monitoring.Prometheus.Status.Configured.ShouldBeFalse();
        monitoring.Grafana.Status.Configured.ShouldBeFalse();
        monitoring.Sentry.Status.Configured.ShouldBeFalse();
        _sentry.Verify(x => x.GetOverviewAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OneBrokenSource_DoesNotHideTheOthers()
    {
        _sentry.Setup(x => x.IsConfigured).Returns(true);
        _sentry.Setup(x => x.GetOverviewAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("token sntrys_secret in the body", null, HttpStatusCode.Unauthorized));
        _grafana.Setup(x => x.IsConfigured).Returns(true);
        _grafana.Setup(x => x.GetOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrafanaOverviewDto(new SourceStatus(true), "12.2.0", [], []));

        var monitoring = await Service().GetMonitoringAsync(TestContext.Current.CancellationToken);

        monitoring.Grafana.Version.ShouldBe("12.2.0");
        monitoring.Sentry.Status.Error.ShouldNotBeNull();
        monitoring.Sentry.Status.Error.ShouldBe("The service answered 401");
        // What the outside service said is never passed on
        monitoring.Sentry.Status.Error.ShouldNotContain("sntrys");
    }

    [Fact]
    public async Task Answers_AreCachedForAMinute()
    {
        _grafana.Setup(x => x.IsConfigured).Returns(true);
        _grafana.Setup(x => x.GetOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrafanaOverviewDto(new SourceStatus(true), "12.2.0", [], []));
        var service = Service();

        await service.GetMonitoringAsync(TestContext.Current.CancellationToken);
        await service.GetMonitoringAsync(TestContext.Current.CancellationToken);

        _grafana.Verify(x => x.GetOverviewAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Prometheus_QuantileWithoutTraffic_IsAGapNotAZero()
    {
        var at = DateTime.UtcNow;
        _prometheus.Setup(x => x.IsConfigured).Returns(true);
        _prometheus.Setup(x => x.QueryRangeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PromSeries(new Dictionary<string, string>(), [new MetricPoint(at, 3), new MetricPoint(at, null)])]);
        _prometheus.Setup(x => x.QueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var overview = (await Service().GetMonitoringAsync(TestContext.Current.CancellationToken)).Prometheus;

        overview.Status.Error.ShouldBeNull();
        overview.P95Ms.ShouldBe(3);
        overview.Up.ShouldBeNull();
    }
}
