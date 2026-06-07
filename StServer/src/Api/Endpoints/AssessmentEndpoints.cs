using Microsoft.EntityFrameworkCore;
using StServer.Application.DTOs.Assessment;
using StServer.Application.Mappers;
using StServer.Infrastructure.Data;
using StServer.Domain.Entities;

namespace StServer.Api.Endpoints;

public static class AssessmentEndpoints
{
    public static void MapAssessmentEndpoints(this WebApplication app)
    {
        var assessmentGroup = app.MapGroup("api/assessments").RequireAuthorization();

        assessmentGroup.MapPost("/start", StartAssessment);
        assessmentGroup.MapGet("/{id}", GetAssessment);
        assessmentGroup.MapPost("/{id}/answer", SubmitAnswer);
        assessmentGroup.MapPatch("/{id}/finish", FinishAssessment);
        assessmentGroup.MapGet("/{id}/results", GetAssessmentResults);

        static async Task<IResult> StartAssessment(AssessmentCreateDto assessmentCreateDto, AppDbContext db)
        {
            var material = await db.Materials
                .Include(m => m.Questions)
                .FirstOrDefaultAsync(m => m.Id == assessmentCreateDto.MaterialId);

            if (material is null) return TypedResults.NotFound();

            var entity = AssessmentMapper.ToEntity(assessmentCreateDto, material.Questions.Count); 

            db.Assessments.Add(entity);
            await db.SaveChangesAsync();

            return TypedResults.Ok(entity.Id);
        }
        
        static async Task<IResult> SubmitAnswer(Guid assessmentId, AssessmentUpdateDto assessmentUpdateDto, AppDbContext db)
        {
            var assessment = await db.Assessments
                .Include(a => a.Results)
                .FirstOrDefaultAsync(a => a.Id == assessmentId);

            if (assessment is null)
                return TypedResults.NotFound();

            var question = await db.Questions.FindAsync(assessmentUpdateDto.QuestionId);

            if (question is null)
                return TypedResults.NotFound();

            var result = new Result
            {
                AssessmentId = assessmentId,
                QuestionId = assessmentUpdateDto.QuestionId,
                UserAnswer = assessmentUpdateDto.Answer,
                IsCorrect = assessmentUpdateDto.Answer == question.Answer,
                AnsweredAt = DateTime.UtcNow
            };

            db.Results.Add(result);

            if (result.IsCorrect)
                assessment.CorrectAnswers++;

            assessment.Score =
                (int)((double)assessment.CorrectAnswers / assessment.TotalQuestions * 100);

            await db.SaveChangesAsync();

            return TypedResults.Ok();
        }
        
        static async Task<IResult> FinishAssessment(Guid id, AppDbContext db)
        {
            var assessment = await db.Assessments.FindAsync(id);

            if (assessment is null)
                return TypedResults.NotFound();

            assessment.FinishedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return TypedResults.Ok();
        }
    }
}