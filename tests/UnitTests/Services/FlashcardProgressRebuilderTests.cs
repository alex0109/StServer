using Application.Interfaces;
using Application.Schedulers;
using Application.Schedulers.Fsrs;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class FlashcardProgressRebuilderTests
{
    private readonly Mock<IFlashcardRepository> _repo = new();
    private readonly FlashcardProgressRebuilder _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _start = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    public FlashcardProgressRebuilderTests()
    {
        var options = new FlashcardSchedulerOptions
        {
            Algorithm = FlashcardAlgorithm.Fsrs,
            Fsrs = new FsrsOptions { EnableFuzzing = false }
        };

        var provider = new FlashcardSchedulerProvider(
            [new BasicFlashcardScheduler(), new FsrsFlashcardScheduler(options)],
            options);

        _sut = new FlashcardProgressRebuilder(_repo.Object, provider);
    }

    private DictionaryEntry CreateEntryWithBasicProgress() => new()
    {
        Id = Guid.NewGuid(),
        Term = "term",
        Translation = "translation",
        CreatedAt = _start,
        Progress = new DictionaryEntryProgress
        {
            UserId = _userId,
            Algorithm = FlashcardAlgorithm.Basic,
            State = FlashcardState.Review,
            ScheduledDays = 3,
            Reps = 2,
            CreatedAt = _start
        }
    };

    private FlashcardReview Review(Guid entryId, ReviewRating rating, DateTime at) => new()
    {
        Id = Guid.NewGuid(),
        UserId = _userId,
        EntryId = entryId,
        Rating = rating,
        ReviewedAt = at
    };

    [Fact]
    public async Task RebuildEntryAsync_ReplaysHistoryWithCurrentAlgorithm()
    {
        var entry = CreateEntryWithBasicProgress();
        var first = Review(entry.Id, ReviewRating.Good, _start);
        var second = Review(entry.Id, ReviewRating.Good, _start.AddMinutes(10));

        _repo.Setup(x => x.GetReviewsForEntriesAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync([second, first]);

        await _sut.RebuildEntryAsync(entry);

        entry.Progress!.Algorithm.ShouldBe(FlashcardAlgorithm.Fsrs);
        entry.Progress.State.ShouldBe(FlashcardState.Review);
        entry.Progress.Stability.ShouldBe(4.4668, 1e-3);
        entry.Progress.Reps.ShouldBe(2);

        // Snapshots are rewritten in replay order, so undo stays consistent
        first.PrevState.ShouldBe(FlashcardState.New);
        first.PrevReps.ShouldBe(0);
        second.PrevState.ShouldBe(FlashcardState.Learning);
        second.PrevStability.ShouldBe(3.173, 1e-6);
    }

    [Fact]
    public async Task RebuildEntryAsync_ResetAsLastAnswer_RemovesProgress()
    {
        var entry = CreateEntryWithBasicProgress();

        _repo.Setup(x => x.GetReviewsForEntriesAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(
            [
                Review(entry.Id, ReviewRating.Good, _start),
                Review(entry.Id, ReviewRating.Manual, _start.AddDays(1))
            ]);

        var progress = entry.Progress!;

        await _sut.RebuildEntryAsync(entry);

        _repo.Verify(x => x.RemoveProgress(progress), Times.Once);
    }

    [Fact]
    public async Task RebuildEntryAsync_ResetInTheMiddle_StartsOverAfterIt()
    {
        var entry = CreateEntryWithBasicProgress();

        _repo.Setup(x => x.GetReviewsForEntriesAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(
            [
                Review(entry.Id, ReviewRating.Easy, _start),
                Review(entry.Id, ReviewRating.Manual, _start.AddDays(1)),
                Review(entry.Id, ReviewRating.Again, _start.AddDays(2))
            ]);

        await _sut.RebuildEntryAsync(entry);

        entry.Progress!.State.ShouldBe(FlashcardState.Learning);
        entry.Progress.Reps.ShouldBe(1);
        entry.Progress.Stability.ShouldBe(0.40255, 1e-6);
    }

    [Fact]
    public async Task RebuildOutdatedAsync_SameCardsAgain_StopsInsteadOfLooping()
    {
        var stuck = CreateEntryWithBasicProgress();

        _repo.Setup(x => x.GetEntriesWithOutdatedProgressAsync(FlashcardAlgorithm.Fsrs, It.IsAny<int>()))
            .ReturnsAsync([stuck]);
        _repo.Setup(x => x.GetReviewsForEntriesAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync([Review(stuck.Id, ReviewRating.Good, _start)]);

        var processed = await _sut.RebuildOutdatedAsync();

        processed.ShouldBe(1);
    }

    [Fact]
    public async Task RebuildOutdatedAsync_PagesThroughAllOutdatedCards()
    {
        var first = CreateEntryWithBasicProgress();
        var second = CreateEntryWithBasicProgress();
        var batch = new List<DictionaryEntry> { first, second };

        _repo.SetupSequence(x => x.GetEntriesWithOutdatedProgressAsync(FlashcardAlgorithm.Fsrs, It.IsAny<int>()))
            .ReturnsAsync(batch)
            .ReturnsAsync([]);
        _repo.Setup(x => x.GetReviewsForEntriesAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(
            [
                Review(first.Id, ReviewRating.Good, _start),
                Review(second.Id, ReviewRating.Easy, _start)
            ]);

        var processed = await _sut.RebuildOutdatedAsync();

        processed.ShouldBe(2);
        first.Progress!.Algorithm.ShouldBe(FlashcardAlgorithm.Fsrs);
        second.Progress!.State.ShouldBe(FlashcardState.Review);
        _repo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }
}
