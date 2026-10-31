using Application.Interfaces;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;

namespace Application.Schedulers;

// What one user changed on top of the global scheduler options
public record SchedulerOverrides(double? DesiredRetention);

// The scheduler as one user sees it. Without overrides the global one is used as is,
// so users who changed nothing get exactly the intervals they got before
public sealed class UserFlashcardScheduler : IFlashcardScheduler
{
    private readonly IFlashcardScheduler _inner;
    private readonly SchedulerOverrides _overrides;

    private UserFlashcardScheduler(IFlashcardScheduler inner, SchedulerOverrides overrides)
    {
        _inner = inner;
        _overrides = overrides;
    }

    public static IFlashcardScheduler For(IFlashcardScheduler scheduler, SchedulerOverrides? overrides) =>
        overrides?.DesiredRetention is null ? scheduler : new UserFlashcardScheduler(scheduler, overrides);

    public FlashcardAlgorithm Algorithm => _inner.Algorithm;

    public FlashcardCard Schedule(FlashcardCard card, ReviewRating rating, DateTime now) =>
        _inner.Schedule(card, rating, now, _overrides);
}
