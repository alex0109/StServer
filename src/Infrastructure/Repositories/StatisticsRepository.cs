using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Attempt;
using Infrastructure.Data;

namespace Infrastructure.Repositories;

public class StatisticsRepository : IStatisticsRepository
{
    private readonly AppDbContext _db;

    public StatisticsRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetMaterialsCountAsync(Guid userId)
    {
        return await _db.Materials.CountAsync(m => m.UserId == userId);
    }

    public async Task<int> GetQuestionsCountAsync(Guid userId)
    {
        return await _db.Questions.CountAsync(q => q.UserId == userId);
    }

    public async Task<List<Attempt>> GetFinishedAttemptsWithResultsAsync(
        Guid userId,
        DateTime? from = null,
        DateTime? to = null)
    {
        var query = _db.Attempts
            .Include(a => a.Results)
            .Where(a => a.UserId == userId && a.AttemptStatus == AttemptStatus.Finished);

        if (from is not null)
            query = query.Where(a => a.FinishedAt >= from);

        if (to is not null)
            query = query.Where(a => a.FinishedAt <= to);

        return await query.ToListAsync();
    }

    public async Task<List<Result>> GetResultsWithQuestionAndTagsAsync(
        Guid userId,
        DateTime? from = null,
        DateTime? to = null)
    {
        var query = _db.Results
            .Include(r => r.Question)
                .ThenInclude(q => q.Material)
                    .ThenInclude(m => m.MaterialTags)
                        .ThenInclude(mt => mt.Tag)
            .Where(r => r.UserId == userId);

        if (from is not null)
            query = query.Where(r => r.AnsweredAt >= from);

        if (to is not null)
            query = query.Where(r => r.AnsweredAt <= to);

        return await query.ToListAsync();
    }
}
