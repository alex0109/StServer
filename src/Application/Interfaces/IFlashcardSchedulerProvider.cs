using Domain.Utility.Flashcard;

namespace Application.Interfaces;

public interface IFlashcardSchedulerProvider
{
    // Scheduler selected in configuration
    IFlashcardScheduler Current { get; }

    IFlashcardScheduler Get(FlashcardAlgorithm algorithm);
}
