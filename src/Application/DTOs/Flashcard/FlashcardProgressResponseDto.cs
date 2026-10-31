using Domain.Utility.Flashcard;

namespace Application.DTOs.Flashcard;

public class FlashcardProgressResponseDto
{
    public FlashcardState State { get; set; }
    public DateTime Due { get; set; }
    public double Stability { get; set; }
    public double Difficulty { get; set; }
    public int ScheduledDays { get; set; }
    public int LearningSteps { get; set; }
    public int Reps { get; set; }
    public int Lapses { get; set; }
    public DateTime? LastReviewAt { get; set; }
}
