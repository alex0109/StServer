using Domain.Entities;
using Domain.Utility.Flashcard;

namespace Application.Interfaces;

public interface IFlashcardRepository
{
    Task<bool> DictionaryExistsAsync(Guid dictionaryId, Guid userId);
    Task<List<DictionaryEntry>> GetActiveEntriesWithProgressAsync(Guid dictionaryId, Guid userId);
    Task<DictionaryEntry?> GetEntryWithProgressAsync(Guid entryId, Guid userId);
    Task<FlashcardSession?> GetSessionAsync(Guid sessionId, Guid userId);
    Task AddSessionAsync(FlashcardSession session);
    Task<FlashcardReview?> GetReviewAsync(Guid reviewId, Guid userId);
    Task<List<FlashcardReview>> GetReviewsForEntriesAsync(List<Guid> entryIds);
    Task<List<DictionaryEntry>> GetEntriesWithOutdatedProgressAsync(FlashcardAlgorithm currentAlgorithm, int take);
    Task<bool> HasLaterReviewAsync(FlashcardReview review);
    Task AddReviewAsync(FlashcardReview review);
    void RemoveReview(FlashcardReview review);
    Task AddProgressAsync(DictionaryEntryProgress progress);
    void RemoveProgress(DictionaryEntryProgress progress);

    Task SaveChangesAsync();
}
