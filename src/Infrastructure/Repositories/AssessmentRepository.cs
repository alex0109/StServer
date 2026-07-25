using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;

namespace Infrastructure.Repositories;

public class AssessmentRepository : IAssessmentRepository
{
    private readonly AppDbContext _db;

    public AssessmentRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Assessment?> GetAssessmentByIdAsync(Guid id, Guid userId)
    {
        return await _db.Assessments.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }
    
    public async Task AddAssessmentAsync(Assessment assessment)
    {
        await _db.Assessments.AddAsync(assessment);
    }
    
    public Task SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }
}