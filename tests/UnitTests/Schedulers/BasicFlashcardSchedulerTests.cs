using Application.Schedulers;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;
using Shouldly;
using Xunit;

namespace UnitTests.Schedulers;

public class BasicFlashcardSchedulerTests
{
    private readonly BasicFlashcardScheduler _sut = new();
    private readonly DateTime _now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ReviewRating.Hard, 1)]
    [InlineData(ReviewRating.Good, 3)]
    [InlineData(ReviewRating.Easy, 7)]
    public void Schedule_NewCard_MovesToReviewWithFirstInterval(ReviewRating rating, int expectedDays)
    {
        var result = _sut.Schedule(FlashcardCard.CreateNew(_now), rating, _now);

        result.State.ShouldBe(FlashcardState.Review);
        result.ScheduledDays.ShouldBe(expectedDays);
        result.Due.ShouldBe(_now.AddDays(expectedDays));
        result.Reps.ShouldBe(1);
        result.LastReviewAt.ShouldBe(_now);
    }

    [Fact]
    public void Schedule_NewCardAgain_MovesToLearningAndIsDueSoon()
    {
        var result = _sut.Schedule(FlashcardCard.CreateNew(_now), ReviewRating.Again, _now);

        result.State.ShouldBe(FlashcardState.Learning);
        result.ScheduledDays.ShouldBe(0);
        result.Due.ShouldBeGreaterThan(_now);
        result.Due.ShouldBeLessThan(_now.AddHours(1));
        result.Lapses.ShouldBe(0);
    }

    [Fact]
    public void Schedule_ReviewCardAgain_CountsLapseAndRelearns()
    {
        var card = new FlashcardCard { State = FlashcardState.Review, ScheduledDays = 10, Reps = 3, Lapses = 1 };

        var result = _sut.Schedule(card, ReviewRating.Again, _now);

        result.State.ShouldBe(FlashcardState.Relearning);
        result.Lapses.ShouldBe(2);
        result.Reps.ShouldBe(4);
    }

    [Fact]
    public void Schedule_ReviewCardGood_GrowsInterval()
    {
        var card = new FlashcardCard { State = FlashcardState.Review, ScheduledDays = 4, Reps = 2 };

        var result = _sut.Schedule(card, ReviewRating.Good, _now);

        result.ScheduledDays.ShouldBe(10);
        result.Due.ShouldBe(_now.AddDays(10));
    }

    [Fact]
    public void Schedule_ReviewCardHard_GrowsAtLeastByOneDay()
    {
        var card = new FlashcardCard { State = FlashcardState.Review, ScheduledDays = 1, Reps = 2 };

        var result = _sut.Schedule(card, ReviewRating.Hard, _now);

        result.ScheduledDays.ShouldBe(2);
    }

    [Fact]
    public void Schedule_KeepsStabilityAndDifficulty()
    {
        var card = new FlashcardCard { State = FlashcardState.Review, ScheduledDays = 3, Stability = 5.5, Difficulty = 4.2 };

        var result = _sut.Schedule(card, ReviewRating.Good, _now);

        result.Stability.ShouldBe(5.5);
        result.Difficulty.ShouldBe(4.2);
    }

    [Fact]
    public void Schedule_ManualRating_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            _sut.Schedule(FlashcardCard.CreateNew(_now), ReviewRating.Manual, _now));
    }
}
