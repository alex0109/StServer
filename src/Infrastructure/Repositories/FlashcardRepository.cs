using Application.Interfaces;
using Domain.Entities;
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

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
