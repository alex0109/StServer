using Application.DTOs.Flashcard;

namespace Application.Interfaces;

public interface IFlashcardService
{
    Task<FlashcardStatsResponseDto?> GetStatsAsync(Guid dictionaryId);

    Task<FlashcardSessionResponseDto?> StartSessionAsync(Guid dictionaryId, FlashcardSessionCreateDto dto);

    // Words from all dictionaries that were studied before and are due by the given time; a word answered
    // since notReviewedSinceUtc waits for tomorrow. Custom mode takes the given words from any dictionary
    Task<FlashcardSessionResponseDto> StartTodaySessionAsync(
        FlashcardSessionCreateDto dto, DateTime dueByUtc, DateTime notReviewedSinceUtc, int maxCards);

    Task<FlashcardSessionSummaryDto?> GetSessionAsync(Guid sessionId);

    Task<FlashcardSessionSummaryDto?> FinishSessionAsync(Guid sessionId);

    Task<FlashcardReviewResponseDto?> AddReviewAsync(Guid sessionId, FlashcardReviewCreateDto dto);

    Task<bool> UndoReviewAsync(Guid sessionId, Guid reviewId);

    Task<bool> ResetProgressAsync(Guid entryId);
}
