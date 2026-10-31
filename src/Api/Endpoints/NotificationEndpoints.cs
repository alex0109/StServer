using Application.DTOs.Notification;
using Application.Interfaces;

namespace Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/notifications")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", GetFeed);
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings);
        group.MapPost("/{notificationId:guid}/read", MarkRead);
        group.MapPost("/read-all", MarkAllRead);

        static async Task<IResult> GetFeed(INotificationService service)
        {
            return TypedResults.Ok(await service.GetFeedAsync());
        }

        static async Task<IResult> GetSettings(INotificationService service)
        {
            return TypedResults.Ok(await service.GetSettingsAsync());
        }

        static async Task<IResult> UpdateSettings(NotificationSettingsUpdateDto dto, INotificationService service)
        {
            return TypedResults.Ok(await service.UpdateSettingsAsync(dto));
        }

        static async Task<IResult> MarkRead(Guid notificationId, INotificationService service)
        {
            return await service.MarkReadAsync(notificationId) ? TypedResults.NoContent() : TypedResults.NotFound();
        }

        static async Task<IResult> MarkAllRead(INotificationService service)
        {
            await service.MarkAllReadAsync();
            return TypedResults.NoContent();
        }
    }
}
