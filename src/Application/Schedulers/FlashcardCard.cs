using Domain.Utility.Flashcard;

namespace Application.Schedulers;

// Scheduler input/output, mirrors the FSRS card
public record FlashcardCard
{
    public FlashcardState State { get; init; } = FlashcardState.New;
    public DateTime Due { get; init; }
    public double Stability { get; init; }
    public double Difficulty { get; init; }
    public int ScheduledDays { get; init; }
    public int LearningSteps { get; init; }
    public int Reps { get; init; }
    public int Lapses { get; init; }
    public DateTime? LastReviewAt { get; init; }

    public static FlashcardCard CreateNew(DateTime now) => new() { Due = now };
}
