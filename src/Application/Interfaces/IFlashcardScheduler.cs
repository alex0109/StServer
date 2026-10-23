using Application.Schedulers;
using Domain.Utility.Review;

namespace Application.Interfaces;

public interface IFlashcardScheduler
{
    FlashcardCard Schedule(FlashcardCard card, ReviewRating rating, DateTime now);
}
