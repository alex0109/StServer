using Application.Observability;
using Infrastructure.Observability;

namespace Api.Common;

// /metrics shows routes, error rates and load, so it is closed: Prometheus (or Grafana Cloud) sends the
// METRICS_TOKEN from the environment as a bearer token or as the basic auth password. Without the
// variable the endpoint doesn't exist at all
public static class MetricsAccess
{
    public const string Path = "/metrics";
    public const string TokenVariable = "METRICS_TOKEN";

    // .NET meters worth a chart, next to our own business counters. Npgsql is left out on purpose:
    // its pool label is the connection string (host, database, user)
    public static readonly HashSet<string> PublishedMeters =
    [
        AppMetrics.MeterName,
        "System.Runtime",
    ];

    public static string? Token()
    {
        var token = Environment.GetEnvironmentVariable(TokenVariable)?.Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    public static IApplicationBuilder UseMetricsAccess(this IApplicationBuilder app, string? token) =>
        app.UseWhen(context => context.Request.Path.Equals(Path, StringComparison.OrdinalIgnoreCase), branch =>
            branch.Use(async (context, next) =>
            {
                if (token is null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                if (!MetricsToken.Allowed(context.Request.Headers.Authorization.ToString(), token))
                {
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                await next();
            }));
}
