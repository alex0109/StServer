using Application.DTOs.Flashcard;

namespace Application.Interfaces;

public interface IFlashcardService
{
    Task<FlashcardStatsResponseDto?> GetStatsAsync(Guid dictionaryId);

    Task<FlashcardSessionResponseDto?> StartSessionAsync(Guid dictionaryId, FlashcardSessionCreateDto dto);

    // Words from all dictionaries: due by the given time first, then at most newLimit new ones.
    // Custom mode takes the given words from any dictionary
    Task<FlashcardSessionResponseDto> StartTodaySessionAsync(
        FlashcardSessionCreateDto dto, DateTime dueByUtc, int newLimit, int maxCards);

    Task<FlashcardSessionSummaryDto?> GetSessionAsync(Guid sessionId);

    Task<FlashcardSessionSummaryDto?> FinishSessionAsync(Guid sessionId);

    Task<FlashcardReviewResponseDto?> AddReviewAsync(Guid sessionId, FlashcardReviewCreateDto dto);

    Task<bool> UndoReviewAsync(Guid sessionId, Guid reviewId);

    Task<bool> ResetProgressAsync(Guid entryId);
}
