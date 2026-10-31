using Application.DTOs.Review;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Flashcard;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly AppDbContext _db;

    public ReviewRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> CountOverdueWordsAsync(Guid userId, DateTime dueBeforeUtc)
    {
        return await _db.DictionaryEntryProgresses
            .CountAsync(p => p.UserId == userId
                             && p.State != FlashcardState.New
                             && p.Due < dueBeforeUtc
                             && !p.Entry.IsArchived);
    }

    public async Task<List<MaterialDueDto>> GetDueQuestionsAsync(Guid userId, DateTime dueByUtc)
    {
        return await _db.Materials
            .Where(m => m.UserId == userId)
            .Select(m => new MaterialDueDto
            {
                MaterialId = m.Id,
                Title = m.Title,
                Due = m.Questions.Count(q => q.IsActive
                                             && q.DeletedAt == null
                                             && q.Progress != null
                                             && q.Progress.State != FlashcardState.New
                                             && q.Progress.Due <= dueByUtc),
            })
            .Where(x => x.Due > 0)
            .OrderByDescending(x => x.Due)
            .ToListAsync();
    }

    public async Task<int> CountOverdueQuestionsAsync(Guid userId, DateTime dueBeforeUtc)
    {
        return await _db.QuestionProgresses
            .CountAsync(p => p.UserId == userId
                             && p.State != FlashcardState.New
                             && p.Due < dueBeforeUtc
                             && p.Question.IsActive
                             && p.Question.DeletedAt == null);
    }

    public async Task<List<Question>> GetDueQuestionsForSessionAsync(Guid userId, DateTime dueByUtc, int take)
    {
        return await _db.Questions
            .Include(q => q.Options)
            .Include(q => q.Material)
            .Include(q => q.Progress)
            .Where(q => q.UserId == userId
                        && q.IsActive
                        && q.DeletedAt == null
                        && q.Progress != null
                        && q.Progress.State != FlashcardState.New
                        && q.Progress.Due <= dueByUtc)
            .OrderBy(q => q.Progress!.Due)
            .Take(take)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<Question?> GetQuestionAsync(Guid questionId, Guid userId)
    {
        return await _db.Questions
            .Include(q => q.Options)
            .Include(q => q.Progress)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.UserId == userId && q.IsActive && q.DeletedAt == null);
    }

    public async Task<QuestionReviewSession?> GetSessionAsync(Guid sessionId, Guid userId)
    {
        return await _db.QuestionReviewSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
    }

    public async Task<bool> IsAnsweredAsync(Guid sessionId, Guid questionId)
    {
        return await _db.QuestionReviews.AnyAsync(r => r.SessionId == sessionId && r.QuestionId == questionId);
    }

    public async Task AddSessionAsync(QuestionReviewSession session)
    {
        await _db.QuestionReviewSessions.AddAsync(session);
    }

    public Task SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }
}
