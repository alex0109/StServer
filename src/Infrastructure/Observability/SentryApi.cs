using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.DTOs.Admin;
using Application.Interfaces;

namespace Infrastructure.Observability;

// Sentry web API with an internal integration token (scopes org:read, project:read, event:read).
// EU organizations live at https://de.sentry.io, others at https://sentry.io
public sealed class SentryApi : ISentryApi
{
    public const string HttpClientName = "Sentry";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Uri _baseUrl;
    private readonly string? _token;
    private readonly string? _org;

    public SentryApi(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = ObservabilityEnv.Url("SENTRY_API_URL") ?? new Uri("https://sentry.io/");
        _token = ObservabilityEnv.Value("SENTRY_API_TOKEN");
        _org = ObservabilityEnv.Value("SENTRY_ORG");
    }

    // The org slug goes into the path, so it must be a slug
    public bool IsConfigured =>
        _token is not null && _org is not null && _org.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public string? Link => _org is null ? null : $"https://{_org}.sentry.io/issues/";

    public async Task<SentryOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var issues = GetAsync(
            $"api/0/organizations/{_org}/issues/?query=is:unresolved&statsPeriod=24h&groupStatsPeriod=24h&sort=freq&limit=15",
            cancellationToken);
        var volume = GetAsync(
            $"api/0/organizations/{_org}/stats_v2/?field=sum(quantity)&category=error&interval=1h&statsPeriod=24h&groupBy=outcome",
            cancellationToken);

        var issueList = (await issues).EnumerateArray().Select(Issue).ToList();
        var (perHour, total) = Volume(await volume);

        return new SentryOverviewDto(new SourceStatus(true, null, Link), total, perHour, issueList);
    }

    private static SentryIssueDto Issue(JsonElement x)
    {
        var trend = x.TryGetProperty("stats", out var stats) && stats.TryGetProperty("24h", out var day)
            ? day.EnumerateArray().Select(point => point[1].GetInt32()).ToList()
            : [];

        return new SentryIssueDto(
            x.GetProperty("id").GetString() ?? "",
            x.GetProperty("shortId").GetString() ?? "",
            x.GetProperty("title").GetString() ?? "",
            x.TryGetProperty("culprit", out var culprit) ? culprit.GetString() : null,
            x.TryGetProperty("level", out var level) ? level.GetString() ?? "error" : "error",
            x.TryGetProperty("project", out var project) && project.TryGetProperty("slug", out var slug) ? slug.GetString() ?? "" : "",
            long.TryParse(x.GetProperty("count").GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0,
            x.TryGetProperty("userCount", out var users) ? users.GetInt32() : 0,
            x.GetProperty("firstSeen").GetDateTime().ToUniversalTime(),
            x.GetProperty("lastSeen").GetDateTime().ToUniversalTime(),
            x.TryGetProperty("permalink", out var link) ? link.GetString() ?? "" : "",
            trend);
    }

    // Accepted errors per hour; dropped ones (rate limits, filters) are not what users saw break
    private static (List<MetricPoint> PerHour, int Total) Volume(JsonElement stats)
    {
        var intervals = stats.GetProperty("intervals").EnumerateArray()
            .Select(x => x.GetDateTime().ToUniversalTime())
            .ToList();

        var accepted = stats.GetProperty("groups").EnumerateArray()
            .FirstOrDefault(g => g.GetProperty("by").TryGetProperty("outcome", out var outcome) && outcome.GetString() == "accepted");

        if (accepted.ValueKind == JsonValueKind.Undefined)
            return (intervals.Select(at => new MetricPoint(at, 0)).ToList(), 0);

        var series = accepted.GetProperty("series").GetProperty("sum(quantity)").EnumerateArray().Select(x => x.GetDouble()).ToList();
        var points = intervals.Zip(series, (at, value) => new MetricPoint(at, value)).ToList();

        return (points, (int)series.Sum());
    }

    private async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUrl, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Sentry request failed", null, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.Clone();
    }
}
