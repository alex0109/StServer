namespace StServer.Api.Endpoints;

public static class AttemptEndpoints
{
    public static void MapAttemptEndpoints(this WebApplication app)
    {
        var assessmentGroup = app.MapGroup("api/attempts").RequireAuthorization();

        assessmentGroup.MapGet("/{attemptId}", GetAttempt);
        assessmentGroup.MapPost("/start", StartAttempt);
        assessmentGroup.MapPost("/{attemptId}/answer", AnswerQuestion);
        assessmentGroup.MapPost("/{attemptId}/submit", SubmitAttempt);
        assessmentGroup.MapGet("/{attemptId}/result", GetResults);
        
        static async Task<IResult> GetAttempt(Guid attemptId, IAttemptService){}
        static async Task<IResult> StartAttempt(Guid questionId, IAttemptService){}
        static async Task<IResult> AnswerQuestion(Guid attemptId, IAttemptService){}
        static async Task<IResult> SubmitAttempt(Guid attemptId, IAttemptService){}
        static async Task<IResult> GetResults(Guid iattemptIdd, IAttemptService){}
    }
}