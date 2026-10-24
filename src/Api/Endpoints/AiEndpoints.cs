using Application.DTOs.Ai;
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
    }
}
