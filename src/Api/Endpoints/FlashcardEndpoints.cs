using Application.DTOs.Flashcard;
using Application.Interfaces;

namespace Api.Endpoints;

public static class FlashcardEndpoints
{
    public static void MapFlashcardEndpoints(this WebApplication app)
    {
        var flashcardGroup = app.MapGroup("api/dictionaries")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        flashcardGroup.MapGet("/{dictionaryId:guid}/flashcards/stats", GetStats);
        flashcardGroup.MapPost("/{dictionaryId:guid}/flashcards/sessions", StartSession);
        flashcardGroup.MapGet("/flashcards/sessions/{sessionId:guid}", GetSession);
        flashcardGroup.MapPost("/flashcards/sessions/{sessionId:guid}/finish", FinishSession);
        flashcardGroup.MapPost("/flashcards/sessions/{sessionId:guid}/reviews", AddReview);
        flashcardGroup.MapDelete("/flashcards/sessions/{sessionId:guid}/reviews/{reviewId:guid}", UndoReview);
        flashcardGroup.MapPost("/entries/{entryId:guid}/progress/reset", ResetProgress);

        static async Task<IResult> GetStats(Guid dictionaryId, IFlashcardService service)
        {
            var result = await service.GetStatsAsync(dictionaryId);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> StartSession(Guid dictionaryId, FlashcardSessionCreateDto sessionCreateDto, IFlashcardService service)
        {
            var result = await service.StartSessionAsync(dictionaryId, sessionCreateDto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Created($"/api/dictionaries/flashcards/sessions/{result.Id}", result);
        }

        static async Task<IResult> GetSession(Guid sessionId, IFlashcardService service)
        {
            var result = await service.GetSessionAsync(sessionId);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> FinishSession(Guid sessionId, IFlashcardService service)
        {
            var result = await service.FinishSessionAsync(sessionId);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> AddReview(Guid sessionId, FlashcardReviewCreateDto reviewCreateDto, IFlashcardService service)
        {
            var result = await service.AddReviewAsync(sessionId, reviewCreateDto);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Created($"/api/dictionaries/flashcards/sessions/{sessionId}/reviews/{result.Id}", result);
        }

        static async Task<IResult> UndoReview(Guid sessionId, Guid reviewId, IFlashcardService service)
        {
            bool result = await service.UndoReviewAsync(sessionId, reviewId);

            if (result) return TypedResults.NoContent();

            return TypedResults.NotFound();
        }

        static async Task<IResult> ResetProgress(Guid entryId, IFlashcardService service)
        {
            bool result = await service.ResetProgressAsync(entryId);

            if (result) return TypedResults.NoContent();

            return TypedResults.NotFound();
        }
    }
}
