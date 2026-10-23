using Application.Schedulers;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;

namespace Application.Interfaces;

public interface IFlashcardScheduler
{
    FlashcardAlgorithm Algorithm { get; }

    FlashcardCard Schedule(FlashcardCard card, ReviewRating rating, DateTime now);
}
