using Application.DTOs.Admin;
using Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Observability;

// One page of monitoring for the owner. Every source is asked separately: a broken or missing one
// shows its own note and never hides the others. Answers are cached for a minute, so the panel
// stays fast and the outside APIs see a request a minute at most
public class ObservabilityService : IObservabilityService
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);
    // A failure is asked again soon: it may have been a cancelled request or a short outage
    private static readonly TimeSpan CacheFailureFor = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    // Fixed queries only: the panel never sends PromQL of its own
    private const string RequestsPerMinute = "sum(rate(http_requests_received_total[5m])) * 60";
    private const string ServerErrorsPerMinute = "sum(rate(http_requests_received_total{code=~\"5..\"}[5m])) * 60 or vector(0)";
    private const string ClientErrorsPerMinute = "sum(rate(http_requests_received_total{code=~\"4..\"}[5m])) * 60 or vector(0)";
    private const string P50Ms = "histogram_quantile(0.5, sum by (le) (rate(http_request_duration_seconds_bucket[5m]))) * 1000";
    private const string P95Ms = "histogram_quantile(0.95, sum by (le) (rate(http_request_duration_seconds_bucket[5m]))) * 1000";
    private const string MemoryMb = "sum(process_working_set_bytes) / 1048576";
    private const string CpuPercent = "sum(rate(process_cpu_seconds_total[5m])) * 100";
    private const string Up = "max(up{job=\"studytracker-api\"})";
    private const string SlowestEndpoints = "topk(6, histogram_quantile(0.95, sum by (le, endpoint) (rate(http_request_duration_seconds_bucket[1h]))) * 1000)";
    private const string BusiestEndpoints = "topk(6, sum by (endpoint) (increase(http_requests_received_total[24h])))";
    private const string AiRequests = "sum by (outcome) (increase(studytracker_ai_requests[24h]))";
    private const string Reviews = "sum by (kind) (increase(studytracker_reviews[24h]))";
    private const string MaterialsCreated = "sum(increase(studytracker_materials_created[24h]))";
    private const string AttemptsFinished = "sum(increase(studytracker_attempts_finished[24h]))";

    private readonly IPrometheusApi _prometheus;
    private readonly IGrafanaApi _grafana;
    private readonly ISentryApi _sentry;
    private readonly IGoogleAnalyticsApi _analytics;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;
    private readonly ILogger<ObservabilityService> _logger;

    public ObservabilityService(
        IPrometheusApi prometheus,
        IGrafanaApi grafana,
        ISentryApi sentry,
        IGoogleAnalyticsApi analytics,
        IMemoryCache cache,
        TimeProvider time,
        ILogger<ObservabilityService> logger)
    {
        _prometheus = prometheus;
        _grafana = grafana;
        _sentry = sentry;
        _analytics = analytics;
        _cache = cache;
        _time = time;
        _logger = logger;
    }

    public async Task<AdminMonitoringDto> GetMonitoringAsync(CancellationToken cancellationToken)
    {
        var prometheus = Cached("observability:prometheus", () => PrometheusAsync(cancellationToken), x => x.Status);
        var grafana = Cached("observability:grafana", () => Safely(
            _grafana.IsConfigured, _grafana.Link, () => _grafana.GetOverviewAsync(cancellationToken),
            status => new GrafanaOverviewDto(status), "Grafana"), x => x.Status);
        var sentry = Cached("observability:sentry", () => Safely(
            _sentry.IsConfigured, _sentry.Link, () => _sentry.GetOverviewAsync(cancellationToken),
            status => new SentryOverviewDto(status), "Sentry"), x => x.Status);

        return new AdminMonitoringDto(await prometheus, await grafana, await sentry);
    }

    public Task<AnalyticsOverviewDto> GetAnalyticsAsync(CancellationToken cancellationToken) =>
        Cached("observability:analytics", () => Safely(
            _analytics.IsConfigured, _analytics.Link, () => _analytics.GetOverviewAsync(cancellationToken),
            status => new AnalyticsOverviewDto(status), "Google Analytics"), x => x.Status);

    private async Task<PrometheusOverviewDto> PrometheusAsync(CancellationToken cancellationToken)
    {
        if (!_prometheus.IsConfigured) return new PrometheusOverviewDto(SourceStatus.Off);

        try
        {
            var end = _time.GetUtcNow().UtcDateTime;
            var start = end - Window;

            async Task<List<MetricPoint>> Range(string query) =>
                (await _prometheus.QueryRangeAsync(query, start, end, Step, cancellationToken)).FirstOrDefault()?.Points ?? [];

            async Task<double?> Now(string query) =>
                (await _prometheus.QueryAsync(query, cancellationToken)).FirstOrDefault()?.Value;

            async Task<List<NamedValue>> Named(string query, string label) =>
                (await _prometheus.QueryAsync(query, cancellationToken))
                    .Where(x => x.Value is double value && !double.IsNaN(value))
                    .Select(x => new NamedValue(x.Labels.GetValueOrDefault(label) ?? "—", Math.Round(x.Value!.Value, 1)))
                    .OrderByDescending(x => x.Value)
                    .ToList();

            var requests = Range(RequestsPerMinute);
            var serverErrors = Range(ServerErrorsPerMinute);
            var clientErrors = Range(ClientErrorsPerMinute);
            var p50 = Range(P50Ms);
            var p95 = Range(P95Ms);
            var memory = Range(MemoryMb);
            var cpu = Range(CpuPercent);

            var activity = new List<NamedValue>();
            foreach (var review in await Named(Reviews, "kind")) activity.Add(review with { Name = $"reviews:{review.Name}" });
            if (await Now(AttemptsFinished) is double attempts) activity.Add(new NamedValue("attempts", Math.Round(attempts)));
            if (await Now(MaterialsCreated) is double materials) activity.Add(new NamedValue("materials", Math.Round(materials)));

            return new PrometheusOverviewDto(
                new SourceStatus(true, null, _prometheus.Link),
                Up: await Now(Up) is double up ? up >= 1 : null,
                RequestsPerMinute: Last(await requests),
                ErrorsPerMinute: Last(await serverErrors),
                P95Ms: Last(await p95),
                MemoryMb: Last(await memory),
                CpuPercent: Last(await cpu),
                Traffic: [new MetricSeries("requests", await requests), new MetricSeries("errors5xx", await serverErrors), new MetricSeries("errors4xx", await clientErrors)],
                Latency: [new MetricSeries("p50", await p50), new MetricSeries("p95", await p95)],
                Resources: [new MetricSeries("memory", await memory), new MetricSeries("cpu", await cpu)],
                SlowestEndpoints: await Named(SlowestEndpoints, "endpoint"),
                BusiestEndpoints: await Named(BusiestEndpoints, "endpoint"),
                AiRequests: await Named(AiRequests, "outcome"),
                Activity: activity);
        }
        catch (Exception exception) when (IsOutsideFailure(exception))
        {
            _logger.LogWarning(exception, "Prometheus query failed");
            return new PrometheusOverviewDto(new SourceStatus(true, Describe(exception), _prometheus.Link));
        }
    }

    private async Task<T> Safely<T>(
        bool configured,
        string? link,
        Func<Task<T>> load,
        Func<SourceStatus, T> empty,
        string name)
    {
        if (!configured) return empty(SourceStatus.Off);

        try
        {
            return await load();
        }
        catch (Exception exception) when (IsOutsideFailure(exception))
        {
            _logger.LogWarning(exception, "{Source} request failed", name);
            return empty(new SourceStatus(true, Describe(exception), link));
        }
    }

    private Task<T> Cached<T>(string key, Func<Task<T>> load, Func<T, SourceStatus> statusOf) =>
        _cache.GetOrCreateAsync(key, async entry =>
        {
            var result = await load();
            entry.AbsoluteExpirationRelativeToNow = statusOf(result).Error is null ? CacheFor : CacheFailureFor;
            return result;
        })!;

    private static double? Last(List<MetricPoint> points) =>
        points.LastOrDefault(x => x.Value is not null)?.Value is double value ? Math.Round(value, 1) : null;

    private static bool IsOutsideFailure(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
            or InvalidOperationException or FormatException or KeyNotFoundException;

    // Status codes and timeouts only: answers of outside APIs may echo tokens or queries
    private static string Describe(Exception exception) => exception switch
    {
        HttpRequestException { StatusCode: { } code } => $"The service answered {(int)code}",
        HttpRequestException => "The service can't be reached",
        TaskCanceledException => "The service took too long to answer",
        _ => "The service gave an answer the panel can't read",
    };
}
