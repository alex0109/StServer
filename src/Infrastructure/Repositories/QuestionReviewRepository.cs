using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Attempt;
using Domain.Utility.Flashcard;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class QuestionReviewRepository : IQuestionReviewRepository
{
    private readonly AppDbContext _db;

    public QuestionReviewRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Question>> GetQuestionsWithProgressAsync(IReadOnlyCollection<Guid> questionIds, Guid userId)
    {
        return await _db.Questions
            .Include(q => q.Progress)
            .Where(q => questionIds.Contains(q.Id) && q.UserId == userId)
            .ToListAsync();
    }

    public async Task<HashSet<Guid>> GetReviewedQuestionIdsAsync(Guid attemptId)
    {
        var ids = await _db.QuestionReviews
            .Where(r => r.AttemptId == attemptId)
            .Select(r => r.QuestionId)
            .ToListAsync();

        return ids.ToHashSet();
    }

    public async Task<List<QuestionReview>> GetReviewsForQuestionsAsync(IReadOnlyCollection<Guid> questionIds)
    {
        return await _db.QuestionReviews
            .Where(r => questionIds.Contains(r.QuestionId))
            .OrderBy(r => r.ReviewedAt)
            .ThenBy(r => r.Id)
            .ToListAsync();
    }

    public async Task<QuestionReview?> GetAttemptReviewAsync(Guid attemptId, Guid questionId)
    {
        return await _db.QuestionReviews
            .FirstOrDefaultAsync(r => r.AttemptId == attemptId && r.QuestionId == questionId);
    }

    public async Task<List<Result>> GetUnreviewedFinishedResultsAsync(int take)
    {
        return await _db.Results
            .Where(r => r.Attempt.AttemptStatus == AttemptStatus.Finished
                        && !_db.QuestionReviews.Any(q => q.AttemptId == r.AttemptId && q.QuestionId == r.QuestionId))
            .OrderBy(r => r.LastModifiedAt ?? r.AnsweredAt)
            .ThenBy(r => r.Id)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Question>> GetQuestionsWithOutdatedProgressAsync(FlashcardAlgorithm currentAlgorithm, int take)
    {
        return await _db.Questions
            .Include(q => q.Progress)
            .Where(q => q.Progress != null && q.Progress.Algorithm != currentAlgorithm)
            .OrderBy(q => q.Id)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<Guid>> GetQuestionIdsWithProgressAsync(Guid userId)
    {
        return await _db.QuestionProgresses
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.QuestionId)
            .Select(p => p.QuestionId)
            .ToListAsync();
    }

    public async Task AddReviewAsync(QuestionReview review)
    {
        await _db.QuestionReviews.AddAsync(review);
    }

    public async Task AddProgressAsync(QuestionProgress progress)
    {
        await _db.QuestionProgresses.AddAsync(progress);
    }

    public void RemoveProgress(QuestionProgress progress)
    {
        _db.QuestionProgresses.Remove(progress);
    }

    public Task SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }
}
