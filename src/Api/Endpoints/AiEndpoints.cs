using Application.DTOs.Ai;
using Application.DTOs.Podcast;
using Application.Interfaces;

namespace Api.Endpoints;

// Every AI feature in one place: removing AI from the API means removing this group
public static class AiEndpoints
{
    public static void MapAiEndpoints(this WebApplication app)
    {
        var aiGroup = app.MapGroup("api/ai")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        aiGroup.MapGet("/usage", GetUsage);
        aiGroup.MapPost("/materials/{materialId:guid}/questions", GenerateQuestions);
        aiGroup.MapPost("/dictionaries/{dictionaryId:guid}/entries", GenerateEntries);
        aiGroup.MapPost("/materials/{materialId:guid}/notes/{noteId:guid}/summary", GenerateSummary);
        aiGroup.MapPost("/attempts/{attemptId:guid}/explanations", ExplainMistakes);
        aiGroup.MapPost("/materials/{materialId:guid}/notes/{noteId:guid}/essay-review", ReviewEssay);
        // Everyday examples for a dictionary word; nothing is saved
        aiGroup.MapPost("/dictionaries/entries/{entryId:guid}/explanation", ExplainWord);
        // A podcast of a material: the script is written now, the voices are recorded in the background
        aiGroup.MapPost("/materials/{materialId:guid}/podcast", GeneratePodcast);
        aiGroup.MapPost("/materials/{materialId:guid}/podcast/retry", RetryPodcast);

        static async Task<IResult> GetUsage(IAiUsageService service, IUserContext user)
        {
            return TypedResults.Ok(await service.GetUsageAsync(user.UserId));
        }

        static async Task<IResult> GenerateQuestions(
            Guid materialId,
            QuestionGenerateDto dto,
            IQuestionGenerationService service,
            CancellationToken cancellationToken)
        {
            var result = await service.GenerateAsync(materialId, dto, cancellationToken);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GenerateEntries(
            Guid dictionaryId,
            DictionaryEntriesGenerateDto dto,
            IDictionaryGenerationService service,
            CancellationToken cancellationToken)
        {
            var result = await service.GenerateAsync(dictionaryId, dto, cancellationToken);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GenerateSummary(
            Guid materialId,
            Guid noteId,
            ISummaryGenerationService service,
            CancellationToken cancellationToken)
        {
            var result = await service.GenerateAsync(materialId, noteId, cancellationToken);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> ExplainMistakes(
            Guid attemptId,
            IMistakeExplanationService service,
            CancellationToken cancellationToken)
        {
            var result = await service.ExplainAsync(attemptId, cancellationToken);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> ReviewEssay(
            Guid materialId,
            Guid noteId,
            IEssayReviewService service,
            CancellationToken cancellationToken)
        {
            var result = await service.ReviewAsync(materialId, noteId, cancellationToken);

            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> ExplainWord(
            Guid entryId,
            IWordExplanationService service,
            CancellationToken cancellationToken)
        {
            var result = await service.ExplainAsync(entryId, cancellationToken);

            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }

        static async Task<IResult> GeneratePodcast(
            Guid materialId,
            PodcastGenerateDto dto,
            IPodcastService service,
            CancellationToken cancellationToken)
        {
            var result = await service.GenerateAsync(materialId, dto, cancellationToken);

            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }

        static async Task<IResult> RetryPodcast(Guid materialId, IPodcastService service)
        {
            var result = await service.RetryAsync(materialId);

            return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }
    }
}
