using Microsoft.EntityFrameworkCore;
using StServer.Infrastructure.Data;
using StServer.Application.DTOs.Question;
using StServer.Application.Mappers;
using StServer.Domain.Entities;

namespace StServer.Api.Endpoints;

public static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this WebApplication app)
    {
     
        var questionGroup = app.MapGroup("api/materials/{materialId}/questions").RequireAuthorization();

        questionGroup.MapGet("/", GetAllQuestions);
        questionGroup.MapGet("/assessment", GetReducedQuestions);
        questionGroup.MapGet("/{id}", GetQuestion);
        questionGroup.MapPost("/open", _);
        questionGroup.MapPost("/truefalse", _);
        questionGroup.MapPost("multiple", _);
        questionGroup.MapPatch("/{id}", UpdateQuestion);
        questionGroup.MapDelete("/{id}", DeleteQuestion);
        
        static async Task<IResult> GetAllQuestions(Guid materialId, AppDbContext db)
        {
            var questions = await db.Questions.Where(x => x.MaterialId == materialId).ToArrayAsync();

            return TypedResults.Ok(questions);
        };
        
        static async Task<IResult> GetReducedQuestions(Guid materialId, AppDbContext db)
        {
            var questions = await db.Questions
                .Where(x => x.MaterialId == materialId)
                .Select(q => new QuestionReducedDto
                {
                    Id = q.Id,
                    Title = q.Title
                })
                .ToArrayAsync();

            return TypedResults.Ok(questions);
        };

        static async Task<IResult> GetQuestion(Guid materialId, Guid id, AppDbContext db)
        {
            var question = await db.Questions.SingleOrDefaultAsync(x => x.MaterialId == materialId && x.Id == id);
            
            if (question is null) return TypedResults.NotFound();
            
            var response = QuestionMapper.ToDto(question);

            return TypedResults.Ok(response);
        };

        static async Task<IResult> CreateQuestion(Guid materialId, QuestionCreateDto questionCreateDto, AppDbContext db)
        {
            var entity = QuestionMapper.ToEntity(questionCreateDto, materialId);
            
            question.Options = dto.Options
                .Select(x => new Option
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Text = x.Text
                })
                .ToList();
            
            db.Questions.Add(entity);
            await db.SaveChangesAsync();

            var response = QuestionMapper.ToDto(entity);
            
            return TypedResults.Created($"materials/{materialId}/questions/{entity.Id}", response);
        };

        static async Task<IResult> UpdateQuestion(Guid materialId, Guid id, QuestionUpdateDto questionUpdateDto, AppDbContext db)
        {
            var question = await db.Questions.FindAsync(id);
            
            if (question is null) return TypedResults.NotFound();

            QuestionMapper.ApplyUpdate(question, questionUpdateDto);

            await db.SaveChangesAsync();
            
            var response = QuestionMapper.ToDto(question);

            return TypedResults.Ok(response);
        }

        static async Task<IResult> DeleteQuestion(Guid materialId, Guid id, AppDbContext db)
        {
            if (await db.Questions.FindAsync(id) is Question question)
            {
                db.Questions.Remove(question);
                await db.SaveChangesAsync();
                return TypedResults.NoContent();
            }

            return TypedResults.NotFound();
        }
        
    }
}