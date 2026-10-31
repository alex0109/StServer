using Application.DTOs.Admin;

namespace Application.Interfaces;

// Read-only clients of outside monitoring services. Keys come only from environment variables;
// IsConfigured is false when they are missing, and nothing is called then

public record PromSeries(IReadOnlyDictionary<string, string> Labels, List<MetricPoint> Points);

public record PromSample(IReadOnlyDictionary<string, string> Labels, double? Value);

public interface IPrometheusApi
{
    bool IsConfigured { get; }
    string? Link { get; }

    Task<List<PromSeries>> QueryRangeAsync(string query, DateTime start, DateTime end, TimeSpan step, CancellationToken cancellationToken);
    Task<List<PromSample>> QueryAsync(string query, CancellationToken cancellationToken);
}

public interface IGrafanaApi
{
    bool IsConfigured { get; }
    string? Link { get; }

    Task<GrafanaOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);
}

public interface ISentryApi
{
    bool IsConfigured { get; }
    string? Link { get; }

    Task<SentryOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);
}

public interface IGoogleAnalyticsApi
{
    bool IsConfigured { get; }
    string? Link { get; }

    Task<AnalyticsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);
}

public interface IObservabilityService
{
    Task<AdminMonitoringDto> GetMonitoringAsync(CancellationToken cancellationToken);
    Task<AnalyticsOverviewDto> GetAnalyticsAsync(CancellationToken cancellationToken);
}
