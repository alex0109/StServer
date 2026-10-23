using Domain.Utility.Flashcard;

namespace Domain.Entities;

// FSRS card state of a dictionary entry, row exists only after the first review
public class DictionaryEntryProgress
{
    public Guid EntryId { get; set; }
    public DictionaryEntry Entry { get; set; } = null!;
    public Guid UserId { get; set; }
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
