using Application.Schedulers;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;

namespace Application.Interfaces;

public interface IFlashcardScheduler
{
    FlashcardAlgorithm Algorithm { get; }

    FlashcardCard Schedule(FlashcardCard card, ReviewRating rating, DateTime now);

    // With one user's settings on top of the global ones; an algorithm without such settings ignores them
    FlashcardCard Schedule(FlashcardCard card, ReviewRating rating, DateTime now, SchedulerOverrides? overrides) =>
        Schedule(card, rating, now);
}
