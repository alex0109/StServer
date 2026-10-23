using Application.Interfaces;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;

namespace Application.Schedulers;

// Simple fixed-interval scheduler, kept as an alternative to FSRS.
// It does not model memory, so Stability and Difficulty stay untouched.
public class BasicFlashcardScheduler : IFlashcardScheduler
{
    private static readonly TimeSpan AgainDelay = TimeSpan.FromMinutes(1);

    public FlashcardAlgorithm Algorithm => FlashcardAlgorithm.Basic;

    public FlashcardCard Schedule(FlashcardCard card, ReviewRating rating, DateTime now)
    {
        if (rating == ReviewRating.Manual)
            throw new ArgumentException("Manual rating can't be scheduled", nameof(rating));

        if (rating == ReviewRating.Again)
        {
            var isLapse = card.State == FlashcardState.Review;

            return card with
            {
                State = isLapse || card.State == FlashcardState.Relearning
                    ? FlashcardState.Relearning
                    : FlashcardState.Learning,
                Due = now + AgainDelay,
                ScheduledDays = 0,
                LearningSteps = 0,
                Reps = card.Reps + 1,
                Lapses = isLapse ? card.Lapses + 1 : card.Lapses,
                LastReviewAt = now,
            };
        }

        var interval = NextIntervalDays(card, rating);

        return card with
        {
            State = FlashcardState.Review,
            Due = now.AddDays(interval),
            ScheduledDays = interval,
            LearningSteps = 0,
            Reps = card.Reps + 1,
            LastReviewAt = now,
        };
    }

    private static int NextIntervalDays(FlashcardCard card, ReviewRating rating)
    {
        // Cards that are not in Review yet start from the first interval
        var previous = card.State == FlashcardState.Review ? Math.Max(card.ScheduledDays, 1) : 0;

        var (first, factor) = rating switch
        {
            ReviewRating.Hard => (1, 1.2),
            ReviewRating.Good => (3, 2.5),
            ReviewRating.Easy => (7, 3.5),
            _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, null)
        };

        if (previous == 0) return first;

        return Math.Max(previous + 1, (int)Math.Round(previous * factor));
    }
}
