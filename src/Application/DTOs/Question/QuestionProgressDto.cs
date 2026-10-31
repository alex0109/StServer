using Domain.Utility.Flashcard;

namespace Application.DTOs.Question;

// Where the question is in spaced repetition; null in the question until its first finished attempt
public class QuestionProgressDto
{
    public FlashcardState State { get; set; }
    public DateTime Due { get; set; }
    public int Reps { get; set; }
    public int Lapses { get; set; }
    // Failed again and again: worth rewording, splitting or a mnemonic
    public bool IsLeech { get; set; }
}
