using Domain.Utility.Review;

namespace Application.DTOs.Flashcard;

public class FlashcardReviewResponseDto
{
    public Guid Id { get; set; }
    public Guid? SessionId { get; set; }
    public Guid EntryId { get; set; }
    public ReviewRating Rating { get; set; }
    public DateTime ReviewedAt { get; set; }
    public FlashcardProgressResponseDto? Progress { get; set; }
}
