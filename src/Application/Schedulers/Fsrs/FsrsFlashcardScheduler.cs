using Application.Interfaces;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;

namespace Application.Schedulers.Fsrs;

// FSRS-5 scheduler with learning and relearning steps, like py-fsrs/ts-fsrs
public class FsrsFlashcardScheduler : IFlashcardScheduler
{
    private static readonly (double Start, double End, double Factor)[] FuzzRanges =
    [
        (2.5, 7.0, 0.15),
        (7.0, 20.0, 0.1),
        (20.0, double.PositiveInfinity, 0.05)
    ];

    private readonly FsrsOptions _options;
    private readonly double[] _w;
    private readonly TimeSpan[] _learningSteps;
    private readonly TimeSpan[] _relearningSteps;
    private readonly Random _random;

    public FsrsFlashcardScheduler(FlashcardSchedulerOptions options)
        : this(options.Fsrs, Random.Shared)
    {
    }

    public FsrsFlashcardScheduler(FsrsOptions options, Random random)
    {
        _options = options;
        _random = random;
        _w = options.Parameters ?? FsrsDefaults.Parameters;

        if (_w.Length != FsrsMath.ParametersCount)
            throw new ArgumentException($"FSRS needs {FsrsMath.ParametersCount} parameters, got {_w.Length}");

        if (options.DesiredRetention is <= 0 or >= 1)
            throw new ArgumentException("FSRS desired retention must be between 0 and 1");

        _learningSteps = ToSteps(options.LearningStepsMinutes ?? FsrsDefaults.LearningStepsMinutes);
        _relearningSteps = ToSteps(options.RelearningStepsMinutes ?? FsrsDefaults.RelearningStepsMinutes);
    }

    public FlashcardAlgorithm Algorithm => FlashcardAlgorithm.Fsrs;

    public FlashcardCard Schedule(FlashcardCard card, ReviewRating rating, DateTime now)
    {
        if (rating == ReviewRating.Manual)
            throw new ArgumentException("Manual rating can't be scheduled", nameof(rating));

        var (stability, difficulty) = NextMemoryState(card, rating, now);

        var (state, step, interval) = card.State switch
        {
            FlashcardState.Review => NextFromReview(rating, stability),
            FlashcardState.Relearning => NextFromSteps(
                card.LearningSteps, rating, stability, _relearningSteps, FlashcardState.Relearning),
            // New cards start at the first learning step
            _ => NextFromSteps(
                card.State == FlashcardState.New ? 0 : card.LearningSteps,
                rating, stability, _learningSteps, FlashcardState.Learning)
        };

        if (state == FlashcardState.Review && _options.EnableFuzzing)
            interval = Fuzz(interval);

        return card with
        {
            State = state,
            Due = now + interval,
            Stability = stability,
            Difficulty = difficulty,
            ScheduledDays = state == FlashcardState.Review ? (int)Math.Round(interval.TotalDays) : 0,
            LearningSteps = step,
            Reps = card.Reps + 1,
            Lapses = card.State == FlashcardState.Review && rating == ReviewRating.Again
                ? card.Lapses + 1
                : card.Lapses,
            LastReviewAt = now,
        };
    }

    private (double Stability, double Difficulty) NextMemoryState(
        FlashcardCard card, ReviewRating rating, DateTime now)
    {
        // Cards without memory state (new, or scheduled by an algorithm that does not model memory)
        // start from the initial values for this rating
        if (card.Stability <= 0 || card.Difficulty <= 0 || card.LastReviewAt is not DateTime lastReviewAt)
        {
            return (FsrsMath.InitialStability(_w, rating), FsrsMath.InitialDifficulty(_w, rating));
        }

        var elapsedDays = Math.Max(0, Math.Floor((now - lastReviewAt).TotalDays));
        var difficulty = FsrsMath.NextDifficulty(_w, card.Difficulty, rating);

        if (elapsedDays < 1)
        {
            return (FsrsMath.ShortTermStability(_w, card.Stability, rating), difficulty);
        }

        var retrievability = FsrsMath.Retrievability(elapsedDays, card.Stability);
        var stability = FsrsMath.NextStability(_w, card.Difficulty, card.Stability, retrievability, rating);

        return (stability, difficulty);
    }

    private (FlashcardState State, int Step, TimeSpan Interval) NextFromReview(
        ReviewRating rating, double stability)
    {
        if (rating == ReviewRating.Again && _relearningSteps.Length > 0)
            return (FlashcardState.Relearning, 0, _relearningSteps[0]);

        return (FlashcardState.Review, 0, ReviewInterval(stability));
    }

    private (FlashcardState State, int Step, TimeSpan Interval) NextFromSteps(
        int step, ReviewRating rating, double stability, TimeSpan[] steps, FlashcardState stepState)
    {
        // No steps configured, or steps were shortened after the card started them
        if (steps.Length == 0 || (step >= steps.Length && rating != ReviewRating.Again))
            return (FlashcardState.Review, 0, ReviewInterval(stability));

        switch (rating)
        {
            case ReviewRating.Again:
                return (stepState, 0, steps[0]);

            case ReviewRating.Hard:
                var hardInterval = step == 0 && steps.Length == 1
                    ? steps[0] * 1.5
                    : step == 0
                        ? (steps[0] + steps[1]) / 2
                        : steps[Math.Min(step, steps.Length - 1)];
                return (stepState, step, hardInterval);

            case ReviewRating.Good:
                return step + 1 >= steps.Length
                    ? (FlashcardState.Review, 0, ReviewInterval(stability))
                    : (stepState, step + 1, steps[step + 1]);

            default:
                return (FlashcardState.Review, 0, ReviewInterval(stability));
        }
    }

    private TimeSpan ReviewInterval(double stability) =>
        TimeSpan.FromDays(FsrsMath.NextIntervalDays(
            stability, _options.DesiredRetention, _options.MaximumIntervalDays));

    // Spreads reviews of cards learned together over a few days
    private TimeSpan Fuzz(TimeSpan interval)
    {
        var days = interval.TotalDays;
        if (days < 2.5) return interval;

        var delta = 1.0;
        foreach (var (start, end, factor) in FuzzRanges)
        {
            delta += factor * Math.Max(Math.Min(days, end) - start, 0);
        }

        var minDays = Math.Max(2, (int)Math.Round(days - delta));
        var maxDays = Math.Min((int)Math.Round(days + delta), _options.MaximumIntervalDays);
        minDays = Math.Min(minDays, maxDays);

        var fuzzed = (int)Math.Floor(_random.NextDouble() * (maxDays - minDays + 1)) + minDays;

        return TimeSpan.FromDays(Math.Min(fuzzed, _options.MaximumIntervalDays));
    }

    private static TimeSpan[] ToSteps(int[] minutes) =>
        minutes.Where(x => x > 0).Select(x => TimeSpan.FromMinutes(x)).ToArray();
}
