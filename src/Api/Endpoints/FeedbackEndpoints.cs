using Application.DTOs.Feedback;
using Application.Interfaces;

namespace Api.Endpoints;

public static class FeedbackEndpoints
{
    public static void MapFeedbackEndpoints(this WebApplication app)
    {
        // Open to everyone: the landing page has the form too
        app.MapPost("api/feedback", SendFeedback)
            .AllowAnonymous()
            .RequireRateLimiting("feedback");

        static async Task<IResult> SendFeedback(
            FeedbackCreateDto dto,
            IFeedbackService service,
            CancellationToken cancellationToken)
        {
            await service.SendAsync(dto, cancellationToken);

            return TypedResults.Accepted((string?)null);
        }
    }
}
