using Application.DTOs.Flashcard;

namespace Application.Interfaces;

public interface IFlashcardService
{
    Task<FlashcardStatsResponseDto?> GetStatsAsync(Guid dictionaryId);

    Task<FlashcardSessionResponseDto?> StartSessionAsync(Guid dictionaryId, FlashcardSessionCreateDto dto);

    Task<FlashcardSessionSummaryDto?> GetSessionAsync(Guid sessionId);

    Task<FlashcardSessionSummaryDto?> FinishSessionAsync(Guid sessionId);

    Task<FlashcardReviewResponseDto?> AddReviewAsync(Guid sessionId, FlashcardReviewCreateDto dto);

    Task<bool> UndoReviewAsync(Guid sessionId, Guid reviewId);

    Task<bool> ResetProgressAsync(Guid entryId);
}
