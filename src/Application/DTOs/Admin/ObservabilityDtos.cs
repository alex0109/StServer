namespace Application.DTOs.Admin;

// Whether a source is set up, and why its data is missing when it is
public record SourceStatus(bool Configured, string? Error = null, string? Link = null)
{
    public static readonly SourceStatus Off = new(false);
}

public record MetricPoint(DateTime At, double? Value);

public record MetricSeries(string Key, List<MetricPoint> Points);

public record NamedValue(string Name, double Value);

public record AdminMonitoringDto(
    PrometheusOverviewDto Prometheus,
    GrafanaOverviewDto Grafana,
    SentryOverviewDto Sentry);

public record PrometheusOverviewDto(
    SourceStatus Status,
    bool? Up = null,
    double? RequestsPerMinute = null,
    double? ErrorsPerMinute = null,
    double? P95Ms = null,
    double? MemoryMb = null,
    double? CpuPercent = null,
    List<MetricSeries>? Traffic = null,
    List<MetricSeries>? Latency = null,
    List<MetricSeries>? Resources = null,
    List<NamedValue>? SlowestEndpoints = null,
    List<NamedValue>? BusiestEndpoints = null,
    List<NamedValue>? AiRequests = null,
    List<NamedValue>? Activity = null);

public record GrafanaOverviewDto(
    SourceStatus Status,
    string? Version = null,
    List<GrafanaDashboardDto>? Dashboards = null,
    List<GrafanaAlertDto>? Alerts = null);

public record GrafanaDashboardDto(string Title, string Url, List<string> Tags);

public record GrafanaAlertDto(string Name, string State, DateTime? ActiveAt, string? Summary);

public record SentryOverviewDto(
    SourceStatus Status,
    int? ErrorsLast24h = null,
    List<MetricPoint>? ErrorsPerHour = null,
    List<SentryIssueDto>? Issues = null);

public record SentryIssueDto(
    string Id,
    string ShortId,
    string Title,
    string? Culprit,
    string Level,
    string Project,
    long Count,
    int Users,
    DateTime FirstSeen,
    DateTime LastSeen,
    string Link,
    List<int> Trend);

public record AnalyticsOverviewDto(
    SourceStatus Status,
    int? ActiveNow = null,
    AnalyticsTotalsDto? Totals = null,
    List<AnalyticsDayDto>? Days = null,
    List<NamedValue>? TopPages = null,
    List<NamedValue>? Countries = null,
    List<NamedValue>? Devices = null,
    List<NamedValue>? Channels = null);

public record AnalyticsTotalsDto(long Users, long NewUsers, long Sessions, long PageViews, double EngagementRate);

public record AnalyticsDayDto(string Date, long Users, long Sessions, long PageViews);
