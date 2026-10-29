using System.Diagnostics;
using System.Reflection;
using Api.Common;
using Application.Ai;
using Application.DTOs.Admin;
using Application.Interfaces;
using Domain.Utility.Admin;
using Hangfire;
using Hangfire.Storage;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public static class AdminEndpoints
{
    private static readonly DateTime StartedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("api/admin")
            .RequireAuthorization(AdminAccess.Policy)
            .RequireRateLimiting("api");

        admin.MapPost("/session", StartSession);
        admin.MapGet("/me", Me);
        admin.MapGet("/stats/overview", Overview);
        admin.MapGet("/stats/ai", Ai);
        admin.MapGet("/health", Health);
        admin.MapGet("/audit", Audit);

        // Before the second factor: tells the panel whether to ask for a TOTP code at all.
        // Any signed-in account may ask; a refusal also emails the owner
        app.MapPost("api/admin/precheck", Precheck)
            .RequireAuthorization()
            .RequireRateLimiting("api");

        static async Task<IResult> Precheck(HttpContext context, AdminAccess.AdminAllowlist allowlist, IAdminAlerts alerts)
        {
            if (allowlist.Contains(AdminAccess.UserId(context.User))) return TypedResults.NoContent();

            await alerts.AccessDeniedAsync("акаунта немає в списку адмінів (вхід в адмінку)");
            return TypedResults.Forbid();
        }

        // Called by the admin panel right after sign-in with 2FA: logs it and emails the owner once per session
        static async Task<IResult> StartSession(IUserContext user, IAdminAlerts alerts, IAdminAudit audit)
        {
            var sessionId = user.Claim(AdminAccess.SessionClaim);
            await audit.RecordAsync(AdminAction.SignIn, details: sessionId is null ? null : $"session {sessionId}");
            await alerts.SignedInAsync(sessionId);

            return TypedResults.NoContent();
        }

        static IResult Me(IUserContext user) =>
            TypedResults.Ok(new AdminMeDto(user.UserId, user.Email, user.Claim(AdminAccess.AssuranceClaim) ?? ""));

        static async Task<IResult> Overview(IAdminStatsService stats) =>
            TypedResults.Ok(await stats.GetOverviewAsync());

        static async Task<IResult> Ai(IAdminStatsService stats) =>
            TypedResults.Ok(await stats.GetAiAsync());

        static async Task<IResult> Audit(IAdminAudit audit, int take = 100) =>
            TypedResults.Ok(await audit.ListAsync(Math.Clamp(take, 1, 500)));

        static async Task<IResult> Health(
            AppDbContext db,
            IEnumerable<IAiProvider> providers,
            AiOptions aiOptions,
            IEmailSender email,
            IWebHostEnvironment environment,
            TimeProvider time)
        {
            var now = time.GetUtcNow().UtcDateTime;

            var stopwatch = Stopwatch.StartNew();
            bool databaseOk;
            try
            {
                databaseOk = await db.Database.CanConnectAsync();
            }
            catch
            {
                databaseOk = false;
            }
            stopwatch.Stop();

            return TypedResults.Ok(new
            {
                Environment = environment.EnvironmentName,
                Version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                ServerTime = now,
                StartedAt,
                UptimeSeconds = (long)(now - StartedAt).TotalSeconds,
                Database = new { Ok = databaseOk, LatencyMs = stopwatch.ElapsedMilliseconds },
                Jobs = JobsHealth(),
                Ai = new
                {
                    aiOptions.Enabled,
                    Providers = providers.Select(x => new { x.Name, x.IsConfigured }),
                },
                Email = new { Configured = email.IsConfigured },
            });
        }
    }

    // Background jobs from Hangfire storage; absent when Hangfire doesn't run (tests)
    private static object JobsHealth()
    {
        try
        {
            var storage = JobStorage.Current;
            var monitoring = storage.GetMonitoringApi();
            using var connection = storage.GetConnection();

            return new
            {
                Available = true,
                Failed = monitoring.FailedCount(),
                Processing = monitoring.ProcessingCount(),
                Scheduled = monitoring.ScheduledCount(),
                Succeeded = monitoring.SucceededListCount(),
                Recurring = connection.GetRecurringJobs()
                    .OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Id,
                        x.Cron,
                        x.LastExecution,
                        x.LastJobState,
                        x.NextExecution,
                        x.Error,
                    }),
            };
        }
        catch (InvalidOperationException)
        {
            return new { Available = false };
        }
    }
}
