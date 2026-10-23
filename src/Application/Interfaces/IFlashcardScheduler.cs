using Application.Schedulers;
using Domain.Utility.Flashcard;

namespace Application.Interfaces;

public interface IFlashcardScheduler
{
    FlashcardCard Schedule(FlashcardCard card, FlashcardRating rating, DateTime now);
}
