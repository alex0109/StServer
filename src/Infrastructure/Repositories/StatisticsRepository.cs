using Microsoft.EntityFrameworkCore;
using Application.DTOs.Statistics;
using Application.Interfaces;
using Domain.Utility.Flashcard;
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
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.AttemptStatus == AttemptStatus.Finished);

        if (from is not null)
            query = query.Where(a => a.FinishedAt >= from);

        if (to is not null)
            query = query.Where(a => a.FinishedAt <= to);

        // Scores need only these fields: answer texts, chosen options and AI explanations stay in the database
        return await query
            .Select(a => new Attempt
            {
                Id = a.Id,
                UserId = a.UserId,
                AssessmentId = a.AssessmentId,
                AttemptStatus = a.AttemptStatus,
                StartedAt = a.StartedAt,
                FinishedAt = a.FinishedAt,
                Results = a.Results
                    .Select(r => new Result
                    {
                        Id = r.Id,
                        AttemptId = r.AttemptId,
                        QuestionId = r.QuestionId,
                        UserId = r.UserId,
                        IsCorrect = r.IsCorrect,
                        Score = r.Score,
                        Weight = r.Weight,
                        TimeSpent = r.TimeSpent,
                        ConfidenceLevel = r.ConfidenceLevel,
                        AnsweredAt = r.AnsweredAt,
                    })
                    .ToList(),
            })
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<List<AnswerStatRow>> GetAnswerStatRowsAsync(Guid userId)
    {
        return await _db.Results
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => new AnswerStatRow(
                r.Question.QuestionDifficulty,
                r.IsCorrect,
                r.TimeSpent,
                r.ConfidenceLevel))
            .ToListAsync();
    }

    // Counted in SQL: one row per tag instead of every answer with its question and material
    public async Task<List<TagAnswerStat>> GetTagAnswerStatsAsync(
        Guid userId,
        DateTime? from = null,
        DateTime? toExclusive = null)
    {
        var results = _db.Results.Where(r => r.UserId == userId);

        if (from is not null)
            results = results.Where(r => r.AnsweredAt >= from);

        if (toExclusive is not null)
            results = results.Where(r => r.AnsweredAt < toExclusive);

        return await results
            .SelectMany(
                r => r.Question.Material.MaterialTags,
                (r, mt) => new { mt.TagId, mt.Tag.Name, r.IsCorrect })
            .GroupBy(x => new { x.TagId, x.Name })
            .Select(g => new TagAnswerStat(
                g.Key.TagId,
                g.Key.Name,
                g.Count(),
                g.Count(x => x.IsCorrect)))
            .ToListAsync();
    }

    public async Task<List<DateTime>> GetActivityTimesAsync(Guid userId, DateTime? from = null)
    {
        var attempts = _db.Attempts
            .Where(a => a.UserId == userId
                        && a.AttemptStatus == AttemptStatus.Finished
                        && a.FinishedAt != null
                        && (from == null || a.FinishedAt >= from))
            .Select(a => a.FinishedAt!.Value);

        var reviews = _db.FlashcardReviews
            .Where(r => r.UserId == userId && (from == null || r.ReviewedAt >= from))
            .Select(r => r.ReviewedAt);

        return await attempts.Concat(reviews).ToListAsync();
    }

    // Same rules as the flashcard stats: archived words don't count, new words are not "due"
    public async Task<List<DictionaryDueDto>> GetDueWordsAsync(Guid userId, DateTime dueBeforeUtc)
    {
        return await _db.Dictionaries
            .Where(d => d.UserId == userId)
            .Select(d => new DictionaryDueDto
            {
                DictionaryId = d.Id,
                MaterialId = d.MaterialId,
                Title = d.Title,
                Due = d.Entries.Count(e =>
                    !e.IsArchived
                    && e.Progress != null
                    && e.Progress.State != FlashcardState.New
                    && e.Progress.Due <= dueBeforeUtc),
                New = d.Entries.Count(e =>
                    !e.IsArchived && (e.Progress == null || e.Progress.State == FlashcardState.New)),
            })
            .Where(x => x.Due > 0 || x.New > 0)
            .OrderByDescending(x => x.Due)
            .ToListAsync();
    }

    public async Task<int> GetReviewsCountAsync(Guid userId, DateTime from, DateTime to)
    {
        return await _db.FlashcardReviews
            .CountAsync(r => r.UserId == userId && r.ReviewedAt >= from && r.ReviewedAt < to);
    }
}
