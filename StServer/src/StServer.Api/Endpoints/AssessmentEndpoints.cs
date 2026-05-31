using Microsoft.EntityFrameworkCore;
using StServer.StServer.Infrastructure.Data;
using StServer.StServer.Application.DTOs;
using StServer.StServer.Application.Mappers;
using StServer.StServer.Domain.Entities;
using System.Diagnostics;

namespace StServer.StServer.Api.Endpoints;

public static class AssessmentEndpoints
{
    public static void MapAssessmentEndpoints(this WebApplication app)
    {
     
        var assessmentGroup = app.MapGroup("api/materials/{materialId}/assessments").RequireAuthorization();

        assessmentGroup.MapGet("/", GetAllAssessments);
        assessmentGroup.MapGet("/{id}", GetAssessment);
        assessmentGroup.MapPost("", CreateAssessment);
        assessmentGroup.MapPatch("/{id}", UpdateAssessment);
        assessmentGroup.MapDelete("/{id}", DeleteAssessment);
        
        static async Task<IResult> GetAllAssessments(Guid materialId, AppDbContext db)
        {
            var assessments = await db.Assessments.Where(x => x.MaterialId == materialId).ToArrayAsync();

            return TypedResults.Ok(assessments);
        };

        static async Task<IResult> GetAssessment(Guid materialId, Guid id, AppDbContext db)
        {
            var assessment = await db.Assessments.SingleOrDefaultAsync(x => x.MaterialId == materialId && x.Id == id);

            return assessment is not null ? TypedResults.Ok(assessment) : TypedResults.NotFound();
        };

        static async Task<IResult> CreateAssessment(Guid materialId, AssessmentItemDto assessmentItemDto, AppDbContext db)
        {
            var entity = AssessmentMapper.ToEntity(assessmentItemDto);
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            
            db.Assessments.Add(entity);
            await db.SaveChangesAsync();
            
            return TypedResults.Created($"materials/{materialId}/assessments/{entity.Id}", AssessmentMapper.ToDto(entity));
        };

        static async Task<IResult> UpdateAssessment(Guid materialId, Guid id, AssessmentUpdateDto assessmentUpdateDto, AppDbContext db)
        {
            var assessment = await db.Assessments.FindAsync(id);
            
            if (assessment is null) return TypedResults.NotFound();

            if (assessmentUpdateDto.Title is not null) assessment.Title = assessmentUpdateDto.Title;
            if (assessmentUpdateDto.Answer is not null) assessment.Answer = assessmentUpdateDto.Answer;
            assessment.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return TypedResults.Ok(AssessmentMapper.ToDto(assessment));
        }

        static async Task<IResult> DeleteAssessment(Guid materialId, Guid id, AppDbContext db)
        {
            if (await db.Assessments.FindAsync(id) is Assessment assessment)
            {
                db.Assessments.Remove(assessment);
                await db.SaveChangesAsync();
                return TypedResults.NoContent();
            }

            return TypedResults.NotFound();
        }
        
    }
}