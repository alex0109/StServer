using Domain.Utility.Flashcard;

namespace Domain.Utility.Review;

// Spaced repetition state of anything a user reviews: a dictionary word or a question.
// Words and questions keep separate tables (real foreign keys, cascades), the scheduling code is shared
public interface IReviewCardState
{
    FlashcardAlgorithm Algorithm { get; set; }
    FlashcardState State { get; set; }
    DateTime Due { get; set; }
    double Stability { get; set; }
    double Difficulty { get; set; }
    int ScheduledDays { get; set; }
    int LearningSteps { get; set; }
    int Reps { get; set; }
    int Lapses { get; set; }
    DateTime? LastReviewAt { get; set; }
    DateTime UpdatedAt { get; set; }
}

// One answer in a review log, with the card state before it (for undo and the FSRS optimizer)
public interface IReviewLogEntry
{
    Guid Id { get; }
    ReviewRating Rating { get; }
    DateTime ReviewedAt { get; }
    double ElapsedDays { get; set; }
    FlashcardState PrevState { get; set; }
    DateTime PrevDue { get; set; }
    double PrevStability { get; set; }
    double PrevDifficulty { get; set; }
    int PrevScheduledDays { get; set; }
    int PrevLearningSteps { get; set; }
    int PrevReps { get; set; }
    int PrevLapses { get; set; }
    DateTime? PrevLastReviewAt { get; set; }
}
