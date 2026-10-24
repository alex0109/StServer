using Application.Interfaces;
using Application.Mappers;

namespace Api.Endpoints;

public static class LimitEndpoints
{
    public static void MapLimitEndpoints(this WebApplication app)
    {
        var limitGroup = app.MapGroup("api/limits")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // Single source of limit values for the client
        limitGroup.MapGet("/", GetLimits);
        // What the current user already uses of every limit
        limitGroup.MapGet("/usage", GetUsage);

        static IResult GetLimits()
        {
            return TypedResults.Ok(UserLimitsMapper.ToDto());
        }

        static async Task<IResult> GetUsage(ILimitsService service)
        {
            return TypedResults.Ok(await service.GetUsageAsync());
        }
    }
}
