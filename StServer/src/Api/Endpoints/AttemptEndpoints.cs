using StServer.Application.DTOs.Attempt;
using StServer.Application.DTOs.Result;
using StServer.Application.Interfaces;

namespace StServer.Api.Endpoints;

public static class AttemptEndpoints
{
    public static void MapAttemptEndpoints(this WebApplication app)
    {
        var assessmentGroup = app.MapGroup("api/attempts")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        assessmentGroup.MapGet("/m/{materialId}", GetFinishedAttempts);
        assessmentGroup.MapGet("/{attemptId}", GetAttempt);
        assessmentGroup.MapPost("/start", StartAttempt);
        assessmentGroup.MapPost("/{attemptId}/answer", AnswerQuestion);
        assessmentGroup.MapPost("/{attemptId}/finish", FinishAttempt);
        assessmentGroup.MapGet("/{attemptId}/results", GetResults);

        static async Task<IResult> GetFinishedAttempts(Guid materialId, IAttemptService service)
        {
            var result = await service.GetFinishedAttempts(materialId);
            
            if (result is null){
                return TypedResults.NotFound();
            }
            
            return TypedResults.Ok(result);
        }
        
        static async Task<IResult> GetAttempt(Guid attemptId, IAttemptService service)
        {
            var result = await service.GetAttempt(attemptId);

            if (result is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result);
        }

        static async Task<IResult> StartAttempt(StartAttemptDto attemptDto, IAttemptService service)
        {
            var result = await service.StartAttempt(attemptDto.AssessmentId);

            if (result is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Created($"/api/attempts/{result}", result);
        }

        static async Task<IResult> AnswerQuestion(Guid attemptId, ResultRequestDto resultDto, IAttemptService service)
        {
            var result = await service.AnswerQuestion(attemptId, resultDto);

            return TypedResults.Ok(result);
        }

        static async Task<IResult> FinishAttempt(Guid attemptId, IAttemptService service)
        {
            var result = await service.FinishAttempt(attemptId);

            if (result is false)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result);
        }

        static async Task<IResult> GetResults(Guid attemptId, IAttemptService service)
        {
            var result = await service.GetResults(attemptId);

            if (result is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result);
        }
    }
}