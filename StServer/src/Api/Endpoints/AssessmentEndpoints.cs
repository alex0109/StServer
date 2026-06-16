using Microsoft.EntityFrameworkCore;
using StServer.Application.DTOs.Assessment;
using StServer.Application.DTOs.Result;
using StServer.Application.Mappers;
using StServer.Infrastructure.Data;

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

        static async Task<IResult> StartAssessment(AssessmentCreateDto dto, AppDbContext db)
        {
            
            var assessment = await db.Assessments.FirstOrDefaultAsync(m => m.MaterialId == dto.MaterialId);

            if (assessment is null)
            {
                var material = await db.Materials
                    .Include(m => m.Questions)
                    .FirstOrDefaultAsync(m => m.Id == dto.MaterialId);

                if (material is null) return TypedResults.NotFound();

                var entity = AssessmentMapper.ToEntity(dto.MaterialId, material.Questions.Count); 

                db.Assessments.Add(entity);
                await db.SaveChangesAsync();

                return TypedResults.Ok(entity.Id);
            }
            
            return TypedResults.Ok(assessment.Id);
        }

        static async Task<IResult> GetAssessment(Guid id, AppDbContext db)
        {
            var item = await db.Assessments.FindAsync(id);
            
            if (item is null) return TypedResults.NotFound();
            
            var response = AssessmentMapper.ToDto(item);
            
            return TypedResults.Ok(response);
        }
        
        static async Task<IResult> SubmitAnswer(Guid id, ResultCreateDto createResultDto, AppDbContext db)
        {
            var assessment = await db.Assessments
                .Include(a => a.Results)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assessment is null)
                return TypedResults.NotFound();

            var question = await db.Questions.FindAsync(createResultDto.QuestionId);

            if (question is null)
                return TypedResults.NotFound();
            
            bool isCorrect = createResultDto.UserAnswer == question.Answer;
            
            var entity = ResultMapper.ToEntity(createResultDto, isCorrect);

            db.Results.Add(entity);

            if (entity.IsCorrect)
                assessment.CorrectAnswers++;

            assessment.Score = (int)((double)assessment.CorrectAnswers / assessment.TotalQuestions * 100);

            await db.SaveChangesAsync();

            return TypedResults.Ok("Answer Submitted!");
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

        static async Task<IResult> GetAssessmentResults(Guid id, AppDbContext db)
        {
            var item = await db.Assessments.Include(a => a.Results).FirstOrDefaultAsync(a => a.Id == id);
            
            if (item is null) return TypedResults.NotFound();
            
            var response = item.Results;
            
            return TypedResults.Ok(response);
        }
    }
}