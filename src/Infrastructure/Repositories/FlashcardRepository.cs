using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Flashcard;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class FlashcardRepository : IFlashcardRepository
{
    private readonly AppDbContext _context;

    public FlashcardRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> DictionaryExistsAsync(Guid dictionaryId, Guid userId)
    {
        return await _context.Dictionaries
            .AnyAsync(d => d.Id == dictionaryId && d.UserId == userId);
    }

    public async Task<List<DictionaryEntry>> GetActiveEntriesWithProgressAsync(Guid dictionaryId, Guid userId)
    {
        return await _context.DictionaryEntries
            .Where(e => e.DictionaryId == dictionaryId && e.Dictionary.UserId == userId && !e.IsArchived)
            .Include(e => e.Progress)
            .OrderBy(e => e.Position)
            .ToListAsync();
    }

    public async Task<DictionaryEntry?> GetEntryWithProgressAsync(Guid entryId, Guid userId)
    {
        return await _context.DictionaryEntries
            .Include(e => e.Progress)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.Dictionary.UserId == userId);
    }

    public async Task<List<DictionaryEntry>> GetDueEntriesAsync(Guid userId, DateTime dueByUtc, int take)
    {
        return await _context.DictionaryEntries
            .Include(e => e.Progress)
            .Where(e => e.Dictionary.UserId == userId
                        && !e.IsArchived
                        && e.Progress != null
                        && e.Progress.State != FlashcardState.New
                        && e.Progress.Due <= dueByUtc)
            .OrderBy(e => e.Progress!.Due)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<DictionaryEntry>> GetNewEntriesAsync(Guid userId, int take)
    {
        if (take <= 0) return [];

        return await _context.DictionaryEntries
            .Include(e => e.Progress)
            .Where(e => e.Dictionary.UserId == userId
                        && !e.IsArchived
                        && (e.Progress == null || e.Progress.State == FlashcardState.New))
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.Position)
            .Take(take)
            .ToListAsync();
    }

    public async Task<List<DictionaryEntry>> GetEntriesWithProgressAsync(Guid userId, IReadOnlyCollection<Guid> entryIds)
    {
        return await _context.DictionaryEntries
            .Include(e => e.Progress)
            .Where(e => entryIds.Contains(e.Id) && e.Dictionary.UserId == userId && !e.IsArchived)
            .ToListAsync();
    }

    public async Task<FlashcardSession?> GetSessionAsync(Guid sessionId, Guid userId)
    {
        return await _context.FlashcardSessions
            .Include(s => s.Reviews)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
    }

    public async Task AddSessionAsync(FlashcardSession session)
    {
        await _context.FlashcardSessions.AddAsync(session);
    }

    public async Task<FlashcardReview?> GetReviewAsync(Guid reviewId, Guid userId)
    {
        return await _context.FlashcardReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);
    }

    public async Task<List<FlashcardReview>> GetReviewsForEntriesAsync(List<Guid> entryIds)
    {
        return await _context.FlashcardReviews
            .Where(r => entryIds.Contains(r.EntryId))
            .OrderBy(r => r.ReviewedAt)
            .ThenBy(r => r.Id)
            .ToListAsync();
    }

    public async Task<List<Guid>> GetEntryIdsWithProgressAsync(Guid userId)
    {
        return await _context.DictionaryEntryProgresses
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.EntryId)
            .Select(p => p.EntryId)
            .ToListAsync();
    }

    public async Task<List<DictionaryEntry>> GetEntriesWithOutdatedProgressAsync(FlashcardAlgorithm currentAlgorithm, int take)
    {
        return await _context.DictionaryEntries
            .Include(e => e.Progress)
            .Where(e => e.Progress != null && e.Progress.Algorithm != currentAlgorithm)
            .OrderBy(e => e.Id)
            .Take(take)
            .ToListAsync();
    }

    public async Task<bool> HasLaterReviewAsync(FlashcardReview review)
    {
        return await _context.FlashcardReviews
            .AnyAsync(r => r.EntryId == review.EntryId
                           && r.Id != review.Id
                           && r.ReviewedAt >= review.ReviewedAt);
    }

    public async Task AddReviewAsync(FlashcardReview review)
    {
        await _context.FlashcardReviews.AddAsync(review);
    }

    public void RemoveReview(FlashcardReview review)
    {
        _context.FlashcardReviews.Remove(review);
    }

    public async Task AddProgressAsync(DictionaryEntryProgress progress)
    {
        await _context.DictionaryEntryProgresses.AddAsync(progress);
    }

    public void RemoveProgress(DictionaryEntryProgress progress)
    {
        _context.DictionaryEntryProgresses.Remove(progress);
    }

    public async Task<int> CloseStaleSessionsAsync(DateTime startedBefore)
    {
        return await _context.FlashcardSessions
            .Where(s => s.FinishedAt == null && s.StartedAt < startedBefore)
            .ExecuteUpdateAsync(x => x.SetProperty(
                s => s.FinishedAt,
                s => s.Reviews.Max(r => (DateTime?)r.ReviewedAt) ?? s.StartedAt));
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
