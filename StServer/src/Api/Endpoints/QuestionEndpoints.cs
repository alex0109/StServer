using Microsoft.EntityFrameworkCore;
using StServer.Infrastructure.Data;
using StServer.Application.DTOs;
using StServer.Application.Mappers;
using StServer.Domain.Entities;

namespace StServer.Api.Endpoints;

public static class QuestionEndpoints
{
    public static void MapQuestionEndpoints(this WebApplication app)
    {
     
        var questionGroup = app.MapGroup("api/materials/{materialId}/questions").RequireAuthorization();

        questionGroup.MapGet("/", GetAllQuestions);
        questionGroup.MapGet("/{id}", GetQuestion);
        questionGroup.MapPost("", CreateQuestion);
        questionGroup.MapPatch("/{id}", UpdateQuestion);
        questionGroup.MapDelete("/{id}", DeleteQuestion);
        
        static async Task<IResult> GetAllQuestions(Guid materialId, AppDbContext db)
        {
            var questions = await db.Questions.Where(x => x.MaterialId == materialId).ToArrayAsync();

            return TypedResults.Ok(questions);
        };

        static async Task<IResult> GetQuestion(Guid materialId, Guid id, AppDbContext db)
        {
            var question = await db.Questions.SingleOrDefaultAsync(x => x.MaterialId == materialId && x.Id == id);

            return question is not null ? TypedResults.Ok(question) : TypedResults.NotFound();
        };

        static async Task<IResult> CreateQuestion(Guid materialId, QuestionItemDto questionItemDto, AppDbContext db)
        {
            var entity = QuestionMapper.ToEntity(questionItemDto);
            entity.MaterialId = materialId;
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            
            db.Questions.Add(entity);
            await db.SaveChangesAsync();
            
            return TypedResults.Created($"materials/{materialId}/questions/{entity.Id}", QuestionMapper.ToDto(entity));
        };

        static async Task<IResult> UpdateQuestion(Guid materialId, Guid id, QuestionUpdateDto questionUpdateDto, AppDbContext db)
        {
            var question = await db.Questions.FindAsync(id);
            
            if (question is null) return TypedResults.NotFound();

            if (questionUpdateDto.Title is not null) question.Title = questionUpdateDto.Title;
            if (questionUpdateDto.Answer is not null) question.Answer = questionUpdateDto.Answer;
            question.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return TypedResults.Ok(QuestionMapper.ToDto(question));
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