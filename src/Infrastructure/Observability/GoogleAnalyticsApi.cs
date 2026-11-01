using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.DTOs.Admin;
using Application.Interfaces;

namespace Infrastructure.Observability;

// Google Analytics Data API (GA4) with a service account that has Viewer access to the property.
// GA_PROPERTY_ID is the numeric property id; GA_CREDENTIALS_JSON is the service account key file,
// as JSON or base64 of it. The key signs a short-lived token request and is never sent itself
public sealed class GoogleAnalyticsApi : IGoogleAnalyticsApi
{
    public const string HttpClientName = "GoogleAnalytics";

    private const string Scope = "https://www.googleapis.com/auth/analytics.readonly";
    private const string DataApi = "https://analyticsdata.googleapis.com/v1beta/properties/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _propertyId;
    private readonly ServiceAccount? _account;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private (string Token, DateTimeOffset Expires)? _token;

    public GoogleAnalyticsApi(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;

        var property = ObservabilityEnv.Value("GA_PROPERTY_ID");
        _propertyId = property is not null && property.All(char.IsAsciiDigit) ? property : null;
        _account = ServiceAccount.Read(ObservabilityEnv.Value("GA_CREDENTIALS_JSON"));
    }

    public bool IsConfigured => _propertyId is not null && _account is not null;

    public string? Link => _propertyId is null ? null : "https://analytics.google.com/analytics/web/";

    public async Task<AnalyticsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var batch = PostAsync(":batchRunReports", new JsonObject
        {
            ["requests"] = new JsonArray(
                // The totals row of the daily report counts each user once for the whole 28 days
                Report(["date"], ["activeUsers", "sessions", "screenPageViews", "newUsers", "engagementRate"], orderByDimension: "date", totals: true, limit: 31),
                Report(["pagePath"], ["screenPageViews"], limit: 8),
                Report(["country"], ["activeUsers"], limit: 8),
                Report(["deviceCategory"], ["activeUsers"], limit: 5),
                Report(["sessionDefaultChannelGroup"], ["sessions"], limit: 6)),
        }, cancellationToken);

        var realtime = PostAsync(":runRealtimeReport", new JsonObject
        {
            ["metrics"] = new JsonArray(new JsonObject { ["name"] = "activeUsers" }),
        }, cancellationToken);

        var answer = await batch;
        var reports = answer.TryGetProperty("reports", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().ToList()
            : [];
        // A report that didn't come back reads as empty
        JsonElement ReportAt(int index) => index < reports.Count ? reports[index] : default;
        var now = await realtime;

        var days = Rows(ReportAt(0))
            .Where(row => row.Dimensions.Count > 0 && row.Metrics.Count >= 3
                          && DateTime.TryParseExact(row.Dimensions[0], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .Select(row => new AnalyticsDayDto(
                DateTime.ParseExact(row.Dimensions[0], "yyyyMMdd", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd"),
                (long)row.Metrics[0], (long)row.Metrics[1], (long)row.Metrics[2]))
            .ToList();

        var totalsRow = Rows(ReportAt(0), "totals").FirstOrDefault(row => row.Metrics.Count >= 5);
        var totals = totalsRow is null
            ? new AnalyticsTotalsDto(0, 0, 0, 0, 0)
            : new AnalyticsTotalsDto(
                Users: (long)totalsRow.Metrics[0],
                NewUsers: (long)totalsRow.Metrics[3],
                Sessions: (long)totalsRow.Metrics[1],
                PageViews: (long)totalsRow.Metrics[2],
                EngagementRate: Math.Round(totalsRow.Metrics[4] * 100, 1));

        return new AnalyticsOverviewDto(
            new SourceStatus(true, null, Link),
            ActiveNow: (int)(Rows(now).FirstOrDefault(row => row.Metrics.Count > 0)?.Metrics[0] ?? 0),
            Totals: totals,
            Days: days,
            TopPages: Named(ReportAt(1)),
            Countries: Named(ReportAt(2)),
            Devices: Named(ReportAt(3)),
            Channels: Named(ReportAt(4)));
    }

    private static JsonObject Report(string[] dimensions, string[] metrics, string? orderByDimension = null, bool totals = false, int limit = 0)
    {
        var report = new JsonObject
        {
            ["dateRanges"] = new JsonArray(new JsonObject { ["startDate"] = "27daysAgo", ["endDate"] = "today" }),
            ["dimensions"] = new JsonArray(dimensions.Select(d => (JsonNode)new JsonObject { ["name"] = d }).ToArray()),
            ["metrics"] = new JsonArray(metrics.Select(m => (JsonNode)new JsonObject { ["name"] = m }).ToArray()),
        };

        if (orderByDimension is not null)
            report["orderBys"] = new JsonArray(new JsonObject { ["dimension"] = new JsonObject { ["dimensionName"] = orderByDimension } });
        else if (dimensions.Length > 0)
            report["orderBys"] = new JsonArray(new JsonObject { ["metric"] = new JsonObject { ["metricName"] = metrics[0] }, ["desc"] = true });

        if (totals) report["metricAggregations"] = new JsonArray("TOTAL");
        if (limit > 0) report["limit"] = limit.ToString(CultureInfo.InvariantCulture);

        return report;
    }

    private sealed record Row(List<string> Dimensions, List<double> Metrics);

    private static List<Row> Rows(JsonElement report, string property = "rows") =>
        report.ValueKind == JsonValueKind.Object && report.TryGetProperty(property, out var rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.EnumerateArray().Select(row => new Row(
                    row.TryGetProperty("dimensionValues", out var d) ? d.EnumerateArray().Select(ValueOf).ToList() : [],
                    row.TryGetProperty("metricValues", out var m)
                        ? m.EnumerateArray()
                            .Select(x => double.TryParse(ValueOf(x), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0)
                            .ToList()
                        : []))
                .ToList()
            : [];

    private static string ValueOf(JsonElement cell) =>
        cell.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static List<NamedValue> Named(JsonElement report) =>
        Rows(report).Where(row => row.Dimensions.Count > 0 && row.Metrics.Count > 0)
            .Select(row => new NamedValue(row.Dimensions[0], row.Metrics[0])).ToList();

    private async Task<JsonElement> PostAsync(string method, JsonObject body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{DataApi}{_propertyId}{method}")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync(cancellationToken));

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Google Analytics request failed", null, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.Clone();
    }

    // OAuth 2.0 for service accounts: a JWT signed with the key is exchanged for an hour-long token
    private async Task<string> AccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is { } cached && cached.Expires > DateTimeOffset.UtcNow.AddMinutes(1)) return cached.Token;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is { } fresh && fresh.Expires > DateTimeOffset.UtcNow.AddMinutes(1)) return fresh.Token;

            var account = _account!;
            using var response = await _httpClientFactory.CreateClient(HttpClientName).PostAsync(
                account.TokenUri,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = account.SignedAssertion(Scope, DateTimeOffset.UtcNow),
                }),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("Google sign-in for Analytics failed", null, response.StatusCode);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var token = document.RootElement.GetProperty("access_token").GetString()!;
            var seconds = document.RootElement.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 3600;

            _token = (token, DateTimeOffset.UtcNow.AddSeconds(seconds));
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    internal sealed record ServiceAccount(string ClientEmail, string PrivateKey, string TokenUri)
    {
        public static ServiceAccount? Read(string? value)
        {
            if (value is null) return null;

            try
            {
                var json = value.TrimStart().StartsWith('{') ? value : Encoding.UTF8.GetString(Convert.FromBase64String(value));
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                return new ServiceAccount(
                    root.GetProperty("client_email").GetString()!,
                    root.GetProperty("private_key").GetString()!,
                    root.TryGetProperty("token_uri", out var uri) ? uri.GetString()! : "https://oauth2.googleapis.com/token");
            }
            catch (Exception exception) when (exception is FormatException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return null;
            }
        }

        public string SignedAssertion(string scope, DateTimeOffset now)
        {
            var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
            var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
            {
                iss = ClientEmail,
                scope,
                aud = TokenUri,
                iat = now.ToUnixTimeSeconds(),
                exp = now.AddMinutes(60).ToUnixTimeSeconds(),
            }));

            using var rsa = RSA.Create();
            rsa.ImportFromPem(PrivateKey);
            var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return $"{header}.{claims}.{Base64Url(signature)}";
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
