using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.DTOs.Admin;
using Application.Interfaces;

namespace Infrastructure.Observability;

// Prometheus HTTP API: a local Prometheus (http://localhost:9090) or Grafana Cloud
// (https://prometheus-prod-....grafana.net/api/prom). PROMETHEUS_USER and PROMETHEUS_TOKEN give
// basic auth (Grafana Cloud: instance id and an access policy token), the token alone is a bearer
public sealed class PrometheusApi : IPrometheusApi
{
    public const string HttpClientName = "Prometheus";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Uri? _baseUrl;
    private readonly AuthenticationHeaderValue? _auth;

    public PrometheusApi(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = ObservabilityEnv.Url("PROMETHEUS_URL");

        var user = ObservabilityEnv.Value("PROMETHEUS_USER");
        var token = ObservabilityEnv.Value("PROMETHEUS_TOKEN");
        _auth = token is null
            ? null
            : user is null
                ? new AuthenticationHeaderValue("Bearer", token)
                : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{token}")));

        Link = ObservabilityEnv.Value("PROMETHEUS_LINK") ?? _baseUrl?.ToString();
    }

    public bool IsConfigured => _baseUrl is not null;

    public string? Link { get; }

    public async Task<List<PromSeries>> QueryRangeAsync(string query, DateTime start, DateTime end, TimeSpan step, CancellationToken cancellationToken)
    {
        var data = await GetAsync(
            $"api/v1/query_range?query={Uri.EscapeDataString(query)}&start={Unix(start)}&end={Unix(end)}&step={(int)step.TotalSeconds}",
            cancellationToken);

        return data.GetProperty("result").EnumerateArray()
            .Select(series => new PromSeries(
                Labels(series),
                series.GetProperty("values").EnumerateArray().Select(Point).ToList()))
            .ToList();
    }

    public async Task<List<PromSample>> QueryAsync(string query, CancellationToken cancellationToken)
    {
        var data = await GetAsync($"api/v1/query?query={Uri.EscapeDataString(query)}", cancellationToken);
        if (data.GetProperty("resultType").GetString() == "scalar")
            return [new PromSample(new Dictionary<string, string>(), Point(data.GetProperty("result")).Value)];

        return data.GetProperty("result").EnumerateArray()
            .Select(sample => new PromSample(Labels(sample), Point(sample.GetProperty("value")).Value))
            .ToList();
    }

    private async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken)
    {
        if (_baseUrl is null) throw new InvalidOperationException("Prometheus is not configured");

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUrl, path));
        request.Headers.Authorization = _auth;

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Prometheus query failed", null, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (document.RootElement.GetProperty("status").GetString() != "success")
            throw new InvalidOperationException("Prometheus refused the query");

        return document.RootElement.GetProperty("data").Clone();
    }

    private static IReadOnlyDictionary<string, string> Labels(JsonElement series) =>
        series.TryGetProperty("metric", out var metric)
            ? metric.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString() ?? "")
            : new Dictionary<string, string>();

    // [unix seconds, "value"]; NaN and infinities (no traffic for a quantile) become gaps
    private static MetricPoint Point(JsonElement pair)
    {
        var at = DateTimeOffset.FromUnixTimeMilliseconds((long)(pair[0].GetDouble() * 1000)).UtcDateTime;
        var parsed = double.TryParse(pair[1].GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                     && double.IsFinite(value);

        return new MetricPoint(at, parsed ? Math.Round(value, 3) : null);
    }

    private static long Unix(DateTime time) => new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)).ToUnixTimeSeconds();
}

// Settings of outside services: environment variables only, never appsettings
internal static class ObservabilityEnv
{
    public static string? Value(string name)
    {
        // Quotes pasted into the hosting settings would become part of the key
        var value = Environment.GetEnvironmentVariable(name)?.Trim().Trim('"', '\'').Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    // Keys travel only over HTTPS; plain HTTP is fine for a Prometheus or Grafana on this machine
    public static Uri? Url(string name)
    {
        var value = Value(name);
        if (value is null || !Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var url)) return null;

        return url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback) ? url : null;
    }
}
