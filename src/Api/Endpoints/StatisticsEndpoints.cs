using Application.Interfaces;

namespace Api.Endpoints;

public static class StatisticsEndpoints
{
    public static void MapStatisticsEndpoints(this WebApplication app)
    {
        var statisticsGroup = app.MapGroup("api/statistics")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        statisticsGroup.MapGet("/overview", GetOverview);
        statisticsGroup.MapGet("/trend", GetAttemptTrend);
        statisticsGroup.MapGet("/difficulty", GetDifficultyBreakdown);
        statisticsGroup.MapGet("/confidence", GetConfidenceCalibration);
        statisticsGroup.MapGet("/tags", GetTagPerformance);
        statisticsGroup.MapGet("/streak", GetStudyStreak);

        static async Task<IResult> GetOverview(IStatisticsService service)
        {
            var result = await service.GetOverviewAsync();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GetAttemptTrend(IStatisticsService service, int days = 30)
        {
            var result = await service.GetAttemptTrendAsync(days);

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GetDifficultyBreakdown(IStatisticsService service)
        {
            var result = await service.GetDifficultyBreakdownAsync();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GetConfidenceCalibration(IStatisticsService service)
        {
            var result = await service.GetConfidenceCalibrationAsync();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GetTagPerformance(IStatisticsService service)
        {
            var result = await service.GetTagPerformanceAsync();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GetStudyStreak(IStatisticsService service)
        {
            var result = await service.GetStudyStreakAsync();

            return TypedResults.Ok(result);
        }
    }
}
