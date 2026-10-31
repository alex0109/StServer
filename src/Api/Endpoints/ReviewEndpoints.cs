using Application.DTOs.Flashcard;
using Application.DTOs.Result;
using Application.DTOs.Review;
using Application.Interfaces;

namespace Api.Endpoints;

// The "Today" queue: words from every dictionary and questions from every material
public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this WebApplication app)
    {
        var reviewGroup = app.MapGroup("api/review")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        reviewGroup.MapGet("/today", GetToday);
        reviewGroup.MapPost("/today/words", StartWords);
        reviewGroup.MapPost("/today/questions", StartQuestions);
        reviewGroup.MapPost("/sessions/{sessionId:guid}/answer", Answer);
        reviewGroup.MapPost("/sessions/{sessionId:guid}/finish", Finish);

        static async Task<IResult> GetToday(IReviewService service) =>
            TypedResults.Ok(await service.GetTodayAsync());

        // Word reviews and undo go through the usual flashcard session endpoints
        static async Task<IResult> StartWords(FlashcardSessionCreateDto dto, IReviewService service)
        {
            var result = await service.StartWordsAsync(dto);
            return TypedResults.Created($"/api/dictionaries/flashcards/sessions/{result.Id}", result);
        }

        static async Task<IResult> StartQuestions(QuestionReviewSessionCreateDto dto, IReviewService service)
        {
            var result = await service.StartQuestionsAsync(dto);
            return TypedResults.Created($"/api/review/sessions/{result.Id}", result);
        }

        static async Task<IResult> Answer(Guid sessionId, ResultRequestDto dto, IReviewService service)
        {
            var result = await service.AnswerQuestionAsync(sessionId, dto);
            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }

        static async Task<IResult> Finish(Guid sessionId, IReviewService service) =>
            await service.FinishQuestionsAsync(sessionId) ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
