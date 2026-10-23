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

        static IResult GetLimits()
        {
            return TypedResults.Ok(UserLimitsMapper.ToDto());
        }
    }
}
