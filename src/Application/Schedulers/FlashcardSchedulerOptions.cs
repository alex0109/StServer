using Application.Schedulers.Fsrs;
using Domain.Utility.Flashcard;

namespace Application.Schedulers;

public class FlashcardSchedulerOptions
{
    public const string SectionName = "Flashcards";

    public FlashcardAlgorithm Algorithm { get; set; } = FlashcardAlgorithm.Fsrs;
    public FsrsOptions Fsrs { get; set; } = new();
}
