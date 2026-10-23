using Application.DTOs.Flashcard;
using Application.Exceptions;
using Application.Interfaces;
using Application.Schedulers;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;
using Moq;
using Shouldly;
using Xunit;

namespace UnitTests.Services;

public class FlashcardServiceTests
{
    private readonly Mock<IFlashcardRepository> _flashcardRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly FlashcardService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _dictionaryId = Guid.NewGuid();

    public FlashcardServiceTests()
    {
        _userContext.Setup(x => x.UserId).Returns(_userId);

        _flashcardRepo
            .Setup(x => x.DictionaryExistsAsync(_dictionaryId, _userId))
            .ReturnsAsync(true);

        _sut = new FlashcardService(
            _flashcardRepo.Object,
            new BasicFlashcardScheduler(),
            _userContext.Object);
    }

    private DictionaryEntry CreateEntry(DictionaryEntryProgress? progress = null, int position = 0)
    {
        var entry = new DictionaryEntry
        {
            Id = Guid.NewGuid(),
            DictionaryId = _dictionaryId,
            Term = $"term-{position}",
            Translation = $"translation-{position}",
            Position = position,
            CreatedAt = DateTime.UtcNow,
        };

        if (progress is not null)
        {
            progress.EntryId = entry.Id;
            progress.UserId = _userId;
            entry.Progress = progress;
        }

        return entry;
    }

    private static DictionaryEntryProgress CreateProgress(FlashcardState state, DateTime due) => new()
    {
        State = state,
        Due = due,
        ScheduledDays = 3,
        Reps = 2,
        LastReviewAt = due.AddDays(-3),
        CreatedAt = DateTime.UtcNow,
    };

    private FlashcardSession CreateSession(DateTime? finishedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = _userId,
        DictionaryId = _dictionaryId,
        Mode = FlashcardSessionMode.Due,
        CardsTotal = 1,
        StartedAt = DateTime.UtcNow,
        FinishedAt = finishedAt,
    };

    [Fact]
    public async Task GetStatsAsync_DictionaryNotFound_ReturnsNull()
    {
        _flashcardRepo
            .Setup(x => x.DictionaryExistsAsync(_dictionaryId, _userId))
            .ReturnsAsync(false);

        var result = await _sut.GetStatsAsync(_dictionaryId);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetStatsAsync_CountsStatesAndDueCards()
    {
        var now = DateTime.UtcNow;
        var entries = new List<DictionaryEntry>
        {
            CreateEntry(),
            CreateEntry(CreateProgress(FlashcardState.Review, now.AddDays(-1))),
            CreateEntry(CreateProgress(FlashcardState.Review, now.AddDays(5))),
            CreateEntry(CreateProgress(FlashcardState.Learning, now.AddMinutes(-5))),
        };

        _flashcardRepo
            .Setup(x => x.GetActiveEntriesWithProgressAsync(_dictionaryId, _userId))
            .ReturnsAsync(entries);

        var result = await _sut.GetStatsAsync(_dictionaryId);

        result.ShouldNotBeNull();
        result.Total.ShouldBe(4);
        result.New.ShouldBe(1);
        result.Review.ShouldBe(2);
        result.Learning.ShouldBe(1);
        result.Due.ShouldBe(2);
    }

    [Fact]
    public async Task StartSessionAsync_DueMode_TakesDueAndNewCardsOnly()
    {
        var now = DateTime.UtcNow;
        var newEntry = CreateEntry(position: 0);
        var dueEntry = CreateEntry(CreateProgress(FlashcardState.Review, now.AddDays(-1)), 1);
        var futureEntry = CreateEntry(CreateProgress(FlashcardState.Review, now.AddDays(3)), 2);

        _flashcardRepo
            .Setup(x => x.GetActiveEntriesWithProgressAsync(_dictionaryId, _userId))
            .ReturnsAsync([newEntry, dueEntry, futureEntry]);

        var result = await _sut.StartSessionAsync(_dictionaryId,
            new FlashcardSessionCreateDto { Mode = FlashcardSessionMode.Due });

        result.ShouldNotBeNull();
        result.CardsTotal.ShouldBe(2);
        result.Cards.Select(c => c.Id).ShouldBe([newEntry.Id, dueEntry.Id], ignoreOrder: true);
        _flashcardRepo.Verify(x => x.AddSessionAsync(It.IsAny<FlashcardSession>()), Times.Once);
        _flashcardRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task StartSessionAsync_DueModeWithLimit_PrefersOverdueCards()
    {
        var now = DateTime.UtcNow;
        var newEntry = CreateEntry(position: 0);
        var overdue = CreateEntry(CreateProgress(FlashcardState.Review, now.AddDays(-10)), 1);
        var due = CreateEntry(CreateProgress(FlashcardState.Review, now.AddDays(-1)), 2);

        _flashcardRepo
            .Setup(x => x.GetActiveEntriesWithProgressAsync(_dictionaryId, _userId))
            .ReturnsAsync([newEntry, due, overdue]);

        var result = await _sut.StartSessionAsync(_dictionaryId,
            new FlashcardSessionCreateDto { Mode = FlashcardSessionMode.Due, Limit = 2 });

        result.ShouldNotBeNull();
        result.Cards.Select(c => c.Id).ShouldBe([overdue.Id, due.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task StartSessionAsync_CustomMode_TakesOnlyRequestedEntries()
    {
        var first = CreateEntry(position: 0);
        var second = CreateEntry(position: 1);

        _flashcardRepo
            .Setup(x => x.GetActiveEntriesWithProgressAsync(_dictionaryId, _userId))
            .ReturnsAsync([first, second]);

        var result = await _sut.StartSessionAsync(_dictionaryId,
            new FlashcardSessionCreateDto { Mode = FlashcardSessionMode.Custom, EntryIds = [second.Id] });

        result.ShouldNotBeNull();
        result.Cards.Single().Id.ShouldBe(second.Id);
    }

    [Fact]
    public async Task StartSessionAsync_CustomModeWithoutIds_ThrowsBadRequest()
    {
        _flashcardRepo
            .Setup(x => x.GetActiveEntriesWithProgressAsync(_dictionaryId, _userId))
            .ReturnsAsync([CreateEntry()]);

        await Should.ThrowAsync<BadRequestException>(() => _sut.StartSessionAsync(_dictionaryId,
            new FlashcardSessionCreateDto { Mode = FlashcardSessionMode.Custom }));
    }

    [Fact]
    public async Task StartSessionAsync_NoCards_ThrowsConflict()
    {
        _flashcardRepo
            .Setup(x => x.GetActiveEntriesWithProgressAsync(_dictionaryId, _userId))
            .ReturnsAsync([CreateEntry(CreateProgress(FlashcardState.Review, DateTime.UtcNow.AddDays(3)))]);

        await Should.ThrowAsync<ConflictException>(() => _sut.StartSessionAsync(_dictionaryId,
            new FlashcardSessionCreateDto { Mode = FlashcardSessionMode.Due }));

        _flashcardRepo.Verify(x => x.AddSessionAsync(It.IsAny<FlashcardSession>()), Times.Never);
    }

    [Fact]
    public async Task AddReviewAsync_NewCard_CreatesProgressAndLogsPreviousState()
    {
        var session = CreateSession();
        var entry = CreateEntry();
        var dto = new FlashcardReviewCreateDto { Id = Guid.NewGuid(), EntryId = entry.Id, Rating = ReviewRating.Good };

        _flashcardRepo.Setup(x => x.GetSessionAsync(session.Id, _userId)).ReturnsAsync(session);
        _flashcardRepo.Setup(x => x.GetEntryWithProgressAsync(entry.Id, _userId)).ReturnsAsync(entry);

        DictionaryEntryProgress? addedProgress = null;
        FlashcardReview? addedReview = null;
        _flashcardRepo
            .Setup(x => x.AddProgressAsync(It.IsAny<DictionaryEntryProgress>()))
            .Callback<DictionaryEntryProgress>(p => addedProgress = p);
        _flashcardRepo
            .Setup(x => x.AddReviewAsync(It.IsAny<FlashcardReview>()))
            .Callback<FlashcardReview>(r => addedReview = r);

        var result = await _sut.AddReviewAsync(session.Id, dto);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(dto.Id);
        result.Progress.ShouldNotBeNull();
        result.Progress.State.ShouldBe(FlashcardState.Review);

        addedProgress.ShouldNotBeNull();
        addedProgress.Reps.ShouldBe(1);

        addedReview.ShouldNotBeNull();
        addedReview.PrevState.ShouldBe(FlashcardState.New);
        addedReview.PrevReps.ShouldBe(0);
        addedReview.ElapsedDays.ShouldBe(0);
        addedReview.SessionId.ShouldBe(session.Id);
    }

    [Fact]
    public async Task AddReviewAsync_ExistingProgress_UpdatesItInPlace()
    {
        var session = CreateSession();
        var progress = CreateProgress(FlashcardState.Review, DateTime.UtcNow.AddDays(-1));
        var entry = CreateEntry(progress);

        _flashcardRepo.Setup(x => x.GetSessionAsync(session.Id, _userId)).ReturnsAsync(session);
        _flashcardRepo.Setup(x => x.GetEntryWithProgressAsync(entry.Id, _userId)).ReturnsAsync(entry);

        await _sut.AddReviewAsync(session.Id,
            new FlashcardReviewCreateDto { Id = Guid.NewGuid(), EntryId = entry.Id, Rating = ReviewRating.Again });

        progress.State.ShouldBe(FlashcardState.Relearning);
        progress.Lapses.ShouldBe(1);
        _flashcardRepo.Verify(x => x.AddProgressAsync(It.IsAny<DictionaryEntryProgress>()), Times.Never);
    }

    [Fact]
    public async Task AddReviewAsync_SameIdTwice_ReturnsExistingWithoutNewReview()
    {
        var session = CreateSession();
        var entry = CreateEntry(CreateProgress(FlashcardState.Review, DateTime.UtcNow.AddDays(2)));
        var existing = new FlashcardReview
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SessionId = session.Id,
            EntryId = entry.Id,
            Rating = ReviewRating.Good,
            ReviewedAt = DateTime.UtcNow,
        };

        _flashcardRepo.Setup(x => x.GetSessionAsync(session.Id, _userId)).ReturnsAsync(session);
        _flashcardRepo.Setup(x => x.GetReviewAsync(existing.Id, _userId)).ReturnsAsync(existing);
        _flashcardRepo.Setup(x => x.GetEntryWithProgressAsync(entry.Id, _userId)).ReturnsAsync(entry);

        var result = await _sut.AddReviewAsync(session.Id,
            new FlashcardReviewCreateDto { Id = existing.Id, EntryId = entry.Id, Rating = ReviewRating.Good });

        result.ShouldNotBeNull();
        result.Id.ShouldBe(existing.Id);
        _flashcardRepo.Verify(x => x.AddReviewAsync(It.IsAny<FlashcardReview>()), Times.Never);
    }

    [Fact]
    public async Task AddReviewAsync_FinishedSession_ThrowsConflict()
    {
        var session = CreateSession(DateTime.UtcNow);

        _flashcardRepo.Setup(x => x.GetSessionAsync(session.Id, _userId)).ReturnsAsync(session);

        await Should.ThrowAsync<ConflictException>(() => _sut.AddReviewAsync(session.Id,
            new FlashcardReviewCreateDto { Id = Guid.NewGuid(), EntryId = Guid.NewGuid(), Rating = ReviewRating.Good }));
    }

    [Fact]
    public async Task AddReviewAsync_ManualRating_ThrowsBadRequest()
    {
        await Should.ThrowAsync<BadRequestException>(() => _sut.AddReviewAsync(Guid.NewGuid(),
            new FlashcardReviewCreateDto { Id = Guid.NewGuid(), EntryId = Guid.NewGuid(), Rating = ReviewRating.Manual }));
    }

    [Fact]
    public async Task AddReviewAsync_EntryFromAnotherDictionary_ReturnsNull()
    {
        var session = CreateSession();
        var entry = CreateEntry();
        entry.DictionaryId = Guid.NewGuid();

        _flashcardRepo.Setup(x => x.GetSessionAsync(session.Id, _userId)).ReturnsAsync(session);
        _flashcardRepo.Setup(x => x.GetEntryWithProgressAsync(entry.Id, _userId)).ReturnsAsync(entry);

        var result = await _sut.AddReviewAsync(session.Id,
            new FlashcardReviewCreateDto { Id = Guid.NewGuid(), EntryId = entry.Id, Rating = ReviewRating.Good });

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UndoReviewAsync_FirstReview_RemovesProgress()
    {
        var session = CreateSession();
        var entry = CreateEntry(CreateProgress(FlashcardState.Review, DateTime.UtcNow.AddDays(3)));
        var review = new FlashcardReview
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SessionId = session.Id,
            EntryId = entry.Id,
            Rating = ReviewRating.Good,
            ReviewedAt = DateTime.UtcNow,
            PrevState = FlashcardState.New,
            PrevReps = 0,
        };

        _flashcardRepo.Setup(x => x.GetReviewAsync(review.Id, _userId)).ReturnsAsync(review);
        _flashcardRepo.Setup(x => x.GetSessionAsync(session.Id, _userId)).ReturnsAsync(session);
        _flashcardRepo.Setup(x => x.HasLaterReviewAsync(review)).ReturnsAsync(false);
        _flashcardRepo.Setup(x => x.GetEntryWithProgressAsync(entry.Id, _userId)).ReturnsAsync(entry);

        var result = await _sut.UndoReviewAsync(session.Id, review.Id);

        result.ShouldBeTrue();
        _flashcardRepo.Verify(x => x.RemoveProgress(entry.Progress!), Times.Once);
        _flashcardRepo.Verify(x => x.RemoveReview(review), Times.Once);
    }

    [Fact]
    public async Task UndoReviewAsync_RepeatedReview_RestoresPreviousState()
    {
        var session = CreateSession();
        var previousDue = DateTime.UtcNow.AddDays(-1);
        var progress = CreateProgress(FlashcardState.Review, DateTime.UtcNow.AddDays(10));
        progress.Reps = 3;
        var entry = CreateEntry(progress);
        var review = new FlashcardReview
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SessionId = session.Id,
            EntryId = entry.Id,
            Rating = ReviewRating.Good,
            ReviewedAt = DateTime.UtcNow,
            PrevState = FlashcardState.Review,
            PrevDue = previousDue,
            PrevScheduledDays = 4,
            PrevReps = 2,
            PrevLastReviewAt = previousDue.AddDays(-4),
        };

        _flashcardRepo.Setup(x => x.GetReviewAsync(review.Id, _userId)).ReturnsAsync(review);
        _flashcardRepo.Setup(x => x.GetSessionAsync(session.Id, _userId)).ReturnsAsync(session);
        _flashcardRepo.Setup(x => x.HasLaterReviewAsync(review)).ReturnsAsync(false);
        _flashcardRepo.Setup(x => x.GetEntryWithProgressAsync(entry.Id, _userId)).ReturnsAsync(entry);

        await _sut.UndoReviewAsync(session.Id, review.Id);

        progress.Due.ShouldBe(previousDue);
        progress.ScheduledDays.ShouldBe(4);
        progress.Reps.ShouldBe(2);
        _flashcardRepo.Verify(x => x.RemoveProgress(It.IsAny<DictionaryEntryProgress>()), Times.Never);
    }

    [Fact]
    public async Task UndoReviewAsync_NotLatestReview_ThrowsConflict()
    {
        var session = CreateSession();
        var review = new FlashcardReview
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SessionId = session.Id,
            EntryId = Guid.NewGuid(),
            ReviewedAt = DateTime.UtcNow,
        };

        _flashcardRepo.Setup(x => x.GetReviewAsync(review.Id, _userId)).ReturnsAsync(review);
        _flashcardRepo.Setup(x => x.GetSessionAsync(session.Id, _userId)).ReturnsAsync(session);
        _flashcardRepo.Setup(x => x.HasLaterReviewAsync(review)).ReturnsAsync(true);

        await Should.ThrowAsync<ConflictException>(() => _sut.UndoReviewAsync(session.Id, review.Id));
        _flashcardRepo.Verify(x => x.RemoveReview(It.IsAny<FlashcardReview>()), Times.Never);
    }

    [Fact]
    public async Task UndoReviewAsync_ReviewFromAnotherSession_ReturnsFalse()
    {
        var review = new FlashcardReview
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SessionId = Guid.NewGuid(),
            EntryId = Guid.NewGuid(),
            ReviewedAt = DateTime.UtcNow,
        };

        _flashcardRepo.Setup(x => x.GetReviewAsync(review.Id, _userId)).ReturnsAsync(review);

        var result = await _sut.UndoReviewAsync(Guid.NewGuid(), review.Id);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task FinishSessionAsync_SetsFinishedAtAndCountsRatings()
    {
        var session = CreateSession();
        session.Reviews =
        [
            new FlashcardReview { Rating = ReviewRating.Good, ReviewedAt = DateTime.UtcNow },
            new FlashcardReview { Rating = ReviewRating.Good, ReviewedAt = DateTime.UtcNow },
            new FlashcardReview { Rating = ReviewRating.Again, ReviewedAt = DateTime.UtcNow },
        ];

        _flashcardRepo.Setup(x => x.GetSessionAsync(session.Id, _userId)).ReturnsAsync(session);

        var result = await _sut.FinishSessionAsync(session.Id);

        result.ShouldNotBeNull();
        result.FinishedAt.ShouldNotBeNull();
        result.Reviewed.ShouldBe(3);
        result.Good.ShouldBe(2);
        result.Again.ShouldBe(1);
        _flashcardRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task FinishSessionAsync_AlreadyFinished_DoesNotSaveAgain()
    {
        var finishedAt = DateTime.UtcNow.AddMinutes(-5);
        var session = CreateSession(finishedAt);

        _flashcardRepo.Setup(x => x.GetSessionAsync(session.Id, _userId)).ReturnsAsync(session);

        var result = await _sut.FinishSessionAsync(session.Id);

        result!.FinishedAt.ShouldBe(finishedAt);
        _flashcardRepo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ResetProgressAsync_RemovesProgressAndWritesManualLog()
    {
        var entry = CreateEntry(CreateProgress(FlashcardState.Review, DateTime.UtcNow.AddDays(3)));

        _flashcardRepo.Setup(x => x.GetEntryWithProgressAsync(entry.Id, _userId)).ReturnsAsync(entry);

        FlashcardReview? log = null;
        _flashcardRepo
            .Setup(x => x.AddReviewAsync(It.IsAny<FlashcardReview>()))
            .Callback<FlashcardReview>(r => log = r);

        var result = await _sut.ResetProgressAsync(entry.Id);

        result.ShouldBeTrue();
        log.ShouldNotBeNull();
        log.Rating.ShouldBe(ReviewRating.Manual);
        log.SessionId.ShouldBeNull();
        log.PrevState.ShouldBe(FlashcardState.Review);
        _flashcardRepo.Verify(x => x.RemoveProgress(entry.Progress!), Times.Once);
    }

    [Fact]
    public async Task ResetProgressAsync_EntryNotFound_ReturnsFalse()
    {
        var result = await _sut.ResetProgressAsync(Guid.NewGuid());

        result.ShouldBeFalse();
    }
}
