using Application.Schedulers;
using Application.Schedulers.Fsrs;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;
using Shouldly;

namespace UnitTests.Schedulers;

public class FsrsFlashcardSchedulerTests
{
    private readonly DateTime _now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    private static FsrsFlashcardScheduler CreateScheduler(double retention = 0.9, int maxInterval = 36500) =>
        new(new FsrsOptions
        {
            DesiredRetention = retention,
            MaximumIntervalDays = maxInterval,
            EnableFuzzing = false
        }, new Random(1));

    private FlashcardCard NewCard() => FlashcardCard.CreateNew(_now);

    [Fact]
    public void Retrievability_AtStabilityDays_IsNinetyPercent()
    {
        FsrsMath.Retrievability(10, 10).ShouldBe(0.9, 1e-9);
    }

    [Fact]
    public void FirstGood_StartsLearningStepsWithInitialMemory()
    {
        var result = CreateScheduler().Schedule(NewCard(), ReviewRating.Good, _now);

        result.State.ShouldBe(FlashcardState.Learning);
        result.LearningSteps.ShouldBe(1);
        result.Due.ShouldBe(_now.AddMinutes(10));
        result.Stability.ShouldBe(3.173, 1e-6);
        result.Difficulty.ShouldBe(5.2824, 1e-3);
        result.ScheduledDays.ShouldBe(0);
        result.Reps.ShouldBe(1);
    }

    [Fact]
    public void FirstAgain_RestartsFirstStep()
    {
        var result = CreateScheduler().Schedule(NewCard(), ReviewRating.Again, _now);

        result.State.ShouldBe(FlashcardState.Learning);
        result.LearningSteps.ShouldBe(0);
        result.Due.ShouldBe(_now.AddMinutes(1));
        result.Stability.ShouldBe(0.40255, 1e-6);
    }

    [Fact]
    public void FirstHard_WaitsBetweenFirstTwoSteps()
    {
        var result = CreateScheduler().Schedule(NewCard(), ReviewRating.Hard, _now);

        result.State.ShouldBe(FlashcardState.Learning);
        result.LearningSteps.ShouldBe(0);
        result.Due.ShouldBe(_now.AddMinutes(5.5));
    }

    [Fact]
    public void FirstEasy_GraduatesWithIntervalEqualToStability()
    {
        var result = CreateScheduler().Schedule(NewCard(), ReviewRating.Easy, _now);

        result.State.ShouldBe(FlashcardState.Review);
        result.Stability.ShouldBe(15.69105, 1e-6);
        result.ScheduledDays.ShouldBe(16);
        result.Due.ShouldBe(_now.AddDays(16));
    }

    [Fact]
    public void GoodOnLastStep_GraduatesUsingShortTermStability()
    {
        var scheduler = CreateScheduler();
        var learning = scheduler.Schedule(NewCard(), ReviewRating.Good, _now);

        var result = scheduler.Schedule(learning, ReviewRating.Good, learning.Due);

        result.State.ShouldBe(FlashcardState.Review);
        result.Stability.ShouldBe(4.4668, 1e-3);
        result.ScheduledDays.ShouldBe(4);
        result.Due.ShouldBe(learning.Due.AddDays(4));
        result.Reps.ShouldBe(2);
    }

    [Fact]
    public void AgainInReview_StartsRelearningAndCountsLapse()
    {
        var scheduler = CreateScheduler();
        var learning = scheduler.Schedule(NewCard(), ReviewRating.Good, _now);
        var review = scheduler.Schedule(learning, ReviewRating.Good, learning.Due);

        var result = scheduler.Schedule(review, ReviewRating.Again, review.Due);

        result.State.ShouldBe(FlashcardState.Relearning);
        result.LearningSteps.ShouldBe(0);
        result.Due.ShouldBe(review.Due.AddMinutes(10));
        result.Lapses.ShouldBe(1);
        result.Stability.ShouldBeLessThan(review.Stability);
        result.Stability.ShouldBe(1.2976, 0.01);
        result.Difficulty.ShouldBeGreaterThan(review.Difficulty);
    }

    [Fact]
    public void GoodInRelearning_ReturnsToReview()
    {
        var scheduler = CreateScheduler();
        var relearning = new FlashcardCard
        {
            State = FlashcardState.Relearning,
            Stability = 2,
            Difficulty = 6,
            LastReviewAt = _now.AddMinutes(-10),
            Reps = 5,
            Lapses = 1
        };

        var result = scheduler.Schedule(relearning, ReviewRating.Good, _now);

        result.State.ShouldBe(FlashcardState.Review);
        result.ScheduledDays.ShouldBeGreaterThanOrEqualTo(1);
        result.Lapses.ShouldBe(1);
    }

    [Fact]
    public void GoodInReview_GrowsStabilityAndInterval()
    {
        var card = new FlashcardCard
        {
            State = FlashcardState.Review,
            Stability = 10,
            Difficulty = 5,
            ScheduledDays = 10,
            LastReviewAt = _now.AddDays(-10),
            Reps = 4
        };

        var result = CreateScheduler().Schedule(card, ReviewRating.Good, _now);

        result.State.ShouldBe(FlashcardState.Review);
        result.Stability.ShouldBeGreaterThan(card.Stability);
        result.ScheduledDays.ShouldBeGreaterThan(card.ScheduledDays);
    }

    [Fact]
    public void Easy_LowersDifficulty_AndAgain_RaisesIt()
    {
        var card = new FlashcardCard
        {
            State = FlashcardState.Review,
            Stability = 10,
            Difficulty = 5,
            LastReviewAt = _now.AddDays(-10)
        };
        var scheduler = CreateScheduler();

        scheduler.Schedule(card, ReviewRating.Easy, _now).Difficulty.ShouldBeLessThan(5);
        scheduler.Schedule(card, ReviewRating.Again, _now).Difficulty.ShouldBeGreaterThan(5);
    }

    [Fact]
    public void HigherDesiredRetention_GivesShorterInterval()
    {
        var card = new FlashcardCard
        {
            State = FlashcardState.Review,
            Stability = 20,
            Difficulty = 5,
            LastReviewAt = _now.AddDays(-20)
        };

        var relaxed = CreateScheduler(0.85).Schedule(card, ReviewRating.Good, _now);
        var strict = CreateScheduler(0.95).Schedule(card, ReviewRating.Good, _now);

        strict.ScheduledDays.ShouldBeLessThan(relaxed.ScheduledDays);
    }

    [Fact]
    public void Interval_IsClampedByMaximum()
    {
        FsrsMath.NextIntervalDays(1_000_000, 0.9, 365).ShouldBe(365);
        FsrsMath.NextIntervalDays(0.01, 0.9, 365).ShouldBe(1);
    }

    [Fact]
    public void CardWithoutMemoryState_StartsFromInitialValues()
    {
        // e.g. scheduled earlier by the basic algorithm
        var card = new FlashcardCard
        {
            State = FlashcardState.Review,
            Stability = 0,
            Difficulty = 0,
            ScheduledDays = 3,
            LastReviewAt = _now.AddDays(-3),
            Reps = 2
        };

        var result = CreateScheduler().Schedule(card, ReviewRating.Good, _now);

        result.Stability.ShouldBe(3.173, 1e-6);
        double.IsNaN(result.Difficulty).ShouldBeFalse();
        result.State.ShouldBe(FlashcardState.Review);
        result.ScheduledDays.ShouldBe(3);
    }

    [Fact]
    public void Fuzzing_KeepsIntervalNearTheNominalOne()
    {
        var scheduler = new FsrsFlashcardScheduler(new FsrsOptions { EnableFuzzing = true }, new Random(42));

        var intervals = Enumerable.Range(0, 200)
            .Select(_ => scheduler.Schedule(NewCard(), ReviewRating.Easy, _now).ScheduledDays)
            .ToList();

        // Nominal 16 days, fuzz range for it is about ±2.6 days
        intervals.ShouldAllBe(x => x >= 13 && x <= 19);
        intervals.Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void ManualRating_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            CreateScheduler().Schedule(NewCard(), ReviewRating.Manual, _now));
    }

    [Fact]
    public void WrongParametersCount_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new FsrsFlashcardScheduler(new FsrsOptions { Parameters = [1, 2, 3] }, new Random()));
    }
}
