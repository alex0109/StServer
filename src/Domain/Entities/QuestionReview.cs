using Domain.Utility.Flashcard;
using Domain.Utility.Review;

namespace Domain.Entities;

// FSRS review log of a question: one row per question per finished attempt (rated from the final answer)
// or per answer in a "Today" review session.
// Prev* fields keep the card state before the review, like FlashcardReview
public class QuestionReview : IReviewLogEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    // The attempt the answer came from; the pair is unique, so finishing twice adds nothing
    public Guid? AttemptId { get; set; }
    public Guid? ResultId { get; set; }
    // Or the "Today" review session it was answered in
    public Guid? SessionId { get; set; }
    public QuestionReviewSession? Session { get; set; }
    public ReviewRating Rating { get; set; }
    public required DateTime ReviewedAt { get; set; }
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
