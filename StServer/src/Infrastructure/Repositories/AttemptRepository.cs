using Microsoft.EntityFrameworkCore;
using StServer.Application.Interfaces;
using StServer.Domain.Entities;
using StServer.Infrastructure.Data;

namespace StServer.Infrastructure.Repositories;

public class AttemptRepository : IAttemptRepository
{
    private readonly AppDbContext _db;

    public AttemptRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Attempt?> GetAttemptByIdAsync(Guid id, Guid userId)
    {
        return await _db.Attempts
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }
    
    public async Task<Attempt?> GetAttemptWithAssessmentByIdAsync(Guid id, Guid userId)
    {
        return await _db.Attempts
            .Include(m => m.Assessment)
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

    public async Task AddAsync(Attempt attempt)
    {
        await _db.Attempts.AddAsync(attempt);
    }
    
    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}