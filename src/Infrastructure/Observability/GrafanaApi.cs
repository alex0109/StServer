using System.Net.Http.Headers;
using System.Text.Json;
using Application.DTOs.Admin;
using Application.Interfaces;

namespace Infrastructure.Observability;

// Grafana HTTP API with a service account token (Viewer role is enough): version, dashboards
// and alerts of Grafana-managed alert rules
public sealed class GrafanaApi : IGrafanaApi
{
    public const string HttpClientName = "Grafana";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Uri? _baseUrl;
    private readonly string? _token;

    public GrafanaApi(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = ObservabilityEnv.Url("GRAFANA_URL");
        _token = ObservabilityEnv.Value("GRAFANA_TOKEN");
    }

    public bool IsConfigured => _baseUrl is not null && _token is not null;

    public string? Link => _baseUrl?.ToString();

    public async Task<GrafanaOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var health = GetAsync("api/health", cancellationToken);
        var search = GetAsync("api/search?type=dash-db&limit=30", cancellationToken);
        var alerts = GetAsync("api/prometheus/grafana/api/v1/alerts", cancellationToken);

        var version = (await health).TryGetProperty("version", out var v) ? v.GetString() : null;

        var dashboards = (await search).EnumerateArray()
            .Select(x => new GrafanaDashboardDto(
                x.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                new Uri(_baseUrl!, (x.TryGetProperty("url", out var url) ? url.GetString() ?? "" : "").TrimStart('/')).ToString(),
                x.TryGetProperty("tags", out var tags) ? tags.EnumerateArray().Select(t => t.GetString() ?? "").ToList() : []))
            .ToList();

        var alertList = AlertsOf(await alerts)
            .Select(x => new GrafanaAlertDto(
                x.TryGetProperty("labels", out var labels) && labels.TryGetProperty("alertname", out var name) ? name.GetString() ?? "alert" : "alert",
                x.TryGetProperty("state", out var state) ? state.GetString() ?? "unknown" : "unknown",
                x.TryGetProperty("activeAt", out var activeAt) && activeAt.TryGetDateTime(out var at) && at.Year > 1 ? at.ToUniversalTime() : null,
                x.TryGetProperty("annotations", out var notes) && notes.TryGetProperty("summary", out var summary) ? summary.GetString() : null))
            // Firing first, then pending
            .OrderBy(x => x.State.Equals("firing", StringComparison.OrdinalIgnoreCase) || x.State.Equals("alerting", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();

        return new GrafanaOverviewDto(new SourceStatus(true, null, Link), version, dashboards, alertList);
    }

    private async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUrl!, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Grafana request failed", null, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.Clone();
    }

    // Grafana versions differ: {"data":{"alerts":[...]}}, a bare list, or nothing without alert rules
    private static IEnumerable<JsonElement> AlertsOf(JsonElement answer)
    {
        if (answer.ValueKind == JsonValueKind.Array) return answer.EnumerateArray();
        if (answer.ValueKind == JsonValueKind.Object
            && answer.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("alerts", out var list) && list.ValueKind == JsonValueKind.Array)
            return list.EnumerateArray();
        return [];
    }
}
