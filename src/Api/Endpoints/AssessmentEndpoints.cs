using Application.Mappers;
using Application.Interfaces;

namespace Api.Endpoints;

public static class AssessmentEndpoints
{
    public static void MapAssessmentEndpoints(this WebApplication app)
    {
        var assessmentGroup = app.MapGroup("api/assessments")
            .RequireRateLimiting("api")
            .RequireAuthorization();

        assessmentGroup.MapPost("/{materialId}/start", StartAssessment);
        assessmentGroup.MapGet("/{materialId}", GetAssessment);

        static async Task<IResult> StartAssessment(Guid materialId, IAssessmentService service)
        {
            var result = await service.StartAssessment(materialId);
            
            return TypedResults.Ok(result);
        }

        static async Task<IResult> GetAssessment(Guid materialId, IAssessmentService service)
        {
            var result = await service.GetAssessment(materialId);
            return TypedResults.Ok(result);
        }
    }
}