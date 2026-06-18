using StServer.Application.DTOs.Question;
using StServer.Application.Interfaces;

namespace StServer.Api.Endpoints;

public static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this WebApplication app)
    {
     
        var questionGroup = app.MapGroup("api/materials/{materialId}/questions").RequireAuthorization();

        questionGroup.MapGet("/", GetAllQuestions);
        questionGroup.MapGet("/assessment", GetReducedQuestions);
        questionGroup.MapGet("/{id}", GetQuestion);
        questionGroup.MapPost("/open", CreateOpenQuestion);
        questionGroup.MapPost("/options", CreateOptionQuestion);
        questionGroup.MapPatch("/{id}", UpdateQuestion);
        questionGroup.MapDelete("/{id}", DeleteQuestion);
        
        static async Task<IResult> GetAllQuestions(Guid materialId, IQuestionService service)
        {
            var result = await service.GetAllQuestionsAsync(materialId);

            return TypedResults.Ok(result);
        };
        
        static async Task<IResult> GetReducedQuestions(Guid materialId, IQuestionService service)
        {
            var result = await service.GetAllReducedQuestionsAsync(materialId);

            return TypedResults.Ok(result);
        };

        static async Task<IResult> GetQuestion(Guid materialId, Guid id, IQuestionService service)
        {
            var result = await service.GetByIdQuestionAsync(materialId, id);
            
            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        };

        static async Task<IResult> CreateOpenQuestion(Guid materialId, OpenQuestionCreateDto questionCreateDto, IQuestionService service)
        {
            var result = await service.CreateOpenQuestionAsync(materialId, questionCreateDto);
            
            
            return TypedResults.Created($"materials/{materialId}/questions/{result.Id}", result);
        };
        
        static async Task<IResult> CreateOptionQuestion(Guid materialId, OptionQuestionCreateDto questionCreateDto, IQuestionService service)
        {
            var result = await service.CreateQuestionWithOptionsAsync(materialId, questionCreateDto);
            
            return TypedResults.Created($"materials/{materialId}/questions/{result.Id}", result);
        };

        static async Task<IResult> UpdateQuestion(Guid materialId, Guid id, QuestionUpdateDto questionUpdateDto, IQuestionService service)
        {
            var result = await service.UpdateQuestionAsync(materialId, id, questionUpdateDto);
            
            if (result is null) return TypedResults.NotFound();

            return TypedResults.Ok(result);
        }

        static async Task<IResult> DeleteQuestion(Guid materialId, Guid id, IQuestionService service)
        {
            bool result = await service.DeleteQuestionAsync(materialId, id);
            
            if (result) return TypedResults.NoContent();
            
            return TypedResults.NotFound();
        }
        
    }
}