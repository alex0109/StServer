using Domain.Utility.Flashcard;
using Domain.Utility.Review;

namespace Domain.Entities;

// FSRS card state of a question, row exists only after the question was answered in a finished attempt
public class QuestionProgress : IReviewCardState
{
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public Guid UserId { get; set; }
    public FlashcardAlgorithm Algorithm { get; set; }
    public FlashcardState State { get; set; }
    public DateTime Due { get; set; }
    public double Stability { get; set; }
    public double Difficulty { get; set; }
    public int ScheduledDays { get; set; }
    public int LearningSteps { get; set; }
    public int Reps { get; set; }
    public int Lapses { get; set; }
    public DateTime? LastReviewAt { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
