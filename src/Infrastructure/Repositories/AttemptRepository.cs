using Microsoft.EntityFrameworkCore;
using StServer.Application.Interfaces;
using StServer.Domain.Entities;
using StServer.Domain.Utility.Attempt;
using StServer.Infrastructure.Data;

namespace StServer.Infrastructure.Repositories;

public class AttemptRepository : IAttemptRepository
{
    private readonly AppDbContext _db;

    public AttemptRepository(AppDbContext db)
    {
        _db = db;
    }
    
    public async Task<List<Attempt>?> GetFinishedAttemptsAsync(Guid materialId, Guid userId)
    {
        return await _db.Attempts
            .Include(m => m.Results)
            .Where(x => x.Assessment.MaterialId == materialId && x.UserId == userId && x.AttemptStatus == AttemptStatus.Finished)
            .ToListAsync();
    }
    
    public async Task<List<Attempt>> GetAbandonedAttemptsAsync()
    {
        var limit = DateTime.UtcNow.AddDays(-1);

        return await _db.Attempts
            .Where(x =>
                x.AttemptStatus == AttemptStatus.InProgress &&
                x.StartedAt < limit)
            .ToListAsync();
    }

    public async Task<Attempt?> GetAttemptByIdAsync(Guid id, Guid userId)
    {
        return await _db.Attempts
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }
    
    public async Task<Attempt?> GetAttemptWithAssessmentByIdAsync(Guid id, Guid userId)
    {
        return await _db.Attempts
            .Include(x => x.Assessment)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }
    
    public async Task<Attempt?> GetAttemptWithResultsByIdAsync(Guid id, Guid userId)
    {
        return await _db.Attempts
            .Include(m => m.Results)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }
    
    public async Task<Attempt?> GetFullAttemptByIdAsync(Guid id, Guid userId)
    {
        return await _db.Attempts
            .Include(m => m.Assessment)
            .Include(m => m.Results)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    public async Task AddAttemptAsync(Attempt attempt)
    {
        await _db.Attempts.AddAsync(attempt);
    }
    
    public async Task AddResultAsync(Result result)
    {
        await _db.Results.AddAsync(result);
    }
    
    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}