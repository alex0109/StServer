using Domain.Utility.Flashcard;
using Domain.Utility.Review;

namespace Domain.Entities;

// FSRS review log. Prev* fields keep the card state before the review,
// they are needed by the FSRS optimizer and to roll the review back on undo
public class FlashcardReview
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? SessionId { get; set; }
    public FlashcardSession? Session { get; set; }
    public Guid EntryId { get; set; }
    public DictionaryEntry Entry { get; set; } = null!;
    public ReviewRating Rating { get; set; }
    public required DateTime ReviewedAt { get; set; }
    public int? DurationMs { get; set; }
    public double ElapsedDays { get; set; }
    public FlashcardState PrevState { get; set; }
    public DateTime PrevDue { get; set; }
    public double PrevStability { get; set; }
    public double PrevDifficulty { get; set; }
    public int PrevScheduledDays { get; set; }
    public int PrevLearningSteps { get; set; }
    public int PrevReps { get; set; }
    public int PrevLapses { get; set; }
    public DateTime? PrevLastReviewAt { get; set; }
}
