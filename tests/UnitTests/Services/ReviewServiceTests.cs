using Application.DTOs.Flashcard;
using Application.DTOs.Question;
using Application.DTOs.Result;
using Application.DTOs.Review;
using Application.DTOs.Statistics;
using Application.Evaluators;
using Application.Exceptions;
using Application.Interfaces;
using Application.Review;
using Application.Schedulers;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Flashcard;
using Domain.Utility.Question;
using Domain.Utility.Result;
using Domain.Utility.Review;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class ReviewServiceTests
{
    // 22:30 UTC on 29 October is already 00:30 on the 30th in Kyiv (UTC+2 after the clock change)
    private static readonly DateTime Now = new(2026, 10, 29, 22, 30, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo Kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IReviewRepository> _repo = new();
    private readonly Mock<IStatisticsRepository> _statistics = new();
    private readonly Mock<IFlashcardService> _flashcards = new();
    private readonly Mock<IQuestionReviewService> _questionReviews = new();
    private readonly Mock<IAnswerEvaluationService> _evaluation = new();
    private readonly ReviewOptions _options = new();
    private readonly ReviewService _sut;

    public ReviewServiceTests()
    {
        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(_userId);
        var zones = new Mock<IUserTimeZoneProvider>();
        zones.Setup(x => x.GetAsync(_userId)).ReturnsAsync(Kyiv);
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));

        _statistics.Setup(x => x.GetDueWordsAsync(_userId, It.IsAny<DateTime>())).ReturnsAsync([]);
        _repo.Setup(x => x.GetDueQuestionsAsync(_userId, It.IsAny<DateTime>())).ReturnsAsync([]);

        _sut = new ReviewService(
            _repo.Object, _statistics.Object, _flashcards.Object, _questionReviews.Object,
            _evaluation.Object, new ConfidenceReviewRatingResolver(), zones.Object, user.Object, _options, time.Object);
    }

    [Fact]
    public async Task Today_EndsAtTheUsersMidnight()
    {
        await _sut.GetTodayAsync();

        // 30 October in Kyiv runs from 22:00 UTC on the 29th to 22:00 UTC on the 30th
        _statistics.Verify(x => x.GetDueWordsAsync(_userId, new DateTime(2026, 10, 30, 22, 0, 0, DateTimeKind.Utc)));
        _repo.Verify(x => x.CountOverdueWordsAsync(_userId, new DateTime(2026, 10, 29, 22, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task Today_CountsAndEstimate()
    {
        _statistics.Setup(x => x.GetDueWordsAsync(_userId, It.IsAny<DateTime>())).ReturnsAsync(
        [
            new DictionaryDueDto { DictionaryId = Guid.NewGuid(), MaterialId = Guid.NewGuid(), Title = "A", Due = 10, New = 50 },
            new DictionaryDueDto { DictionaryId = Guid.NewGuid(), MaterialId = Guid.NewGuid(), Title = "B", Due = 5, New = 0 },
        ]);
        _repo.Setup(x => x.GetDueQuestionsAsync(_userId, It.IsAny<DateTime>())).ReturnsAsync(
            [new MaterialDueDto { MaterialId = Guid.NewGuid(), Title = "M", Due = 4 }]);

        var today = await _sut.GetTodayAsync();

        today.DueWords.ShouldBe(15);
        today.NewWords.ShouldBe(50);
        today.DueQuestions.ShouldBe(4);
        // 15 due + 20 new words at 8 s, 4 questions at 30 s = 400 s → 7 minutes
        today.WordsInSession.ShouldBe(35);
        today.EstimatedMinutes.ShouldBe(7);
        today.LocalDate.ShouldBe("2026-10-30");
    }

    [Fact]
    public async Task NothingDue_NoQuestionSession()
    {
        _repo.Setup(x => x.GetDueQuestionsForSessionAsync(_userId, It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync([]);

        await Should.ThrowAsync<ConflictException>(() => _sut.StartQuestionsAsync(new QuestionReviewSessionCreateDto()));
    }

    [Fact]
    public async Task Answer_IsScheduledOnce()
    {
        var sessionId = Guid.NewGuid();
        var question = new Question
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = "Capital of Norway?",
            Answer = "Oslo",
            QuestionType = QuestionType.Open,
            CreatedAt = Now,
        };
        _repo.Setup(x => x.GetSessionAsync(sessionId, _userId))
            .ReturnsAsync(new QuestionReviewSession { Id = sessionId, UserId = _userId, StartedAt = Now });
        _repo.Setup(x => x.GetQuestionAsync(question.Id, _userId)).ReturnsAsync(question);
        _evaluation.Setup(x => x.EvaluateAnswerAsync(question.Title, "Oslo", "oslo", default))
            .ReturnsAsync(new AnswerEvaluationResult { IsCorrect = true, Score = 1, Method = EvaluationMethod.Exact });
        _questionReviews.Setup(x => x.ReviewInSessionAsync(question, sessionId, ReviewRating.Good))
            .ReturnsAsync(new QuestionProgress { QuestionId = question.Id, State = FlashcardState.Review, Due = Now.AddDays(3), CreatedAt = Now });

        var result = await _sut.AnswerQuestionAsync(sessionId, new ResultRequestDto { QuestionId = question.Id, UserAnswer = "oslo" });

        result.ShouldNotBeNull();
        result.IsCorrect.ShouldBeTrue();
        result.CorrectAnswer.ShouldBe("Oslo");
        result.NextDue.ShouldBe(Now.AddDays(3));
        _questionReviews.Verify(x => x.ReviewInSessionAsync(question, sessionId, ReviewRating.Good), Times.Once);

        // The same question again in the same session is refused
        _repo.Setup(x => x.IsAnsweredAsync(sessionId, question.Id)).ReturnsAsync(true);
        await Should.ThrowAsync<ConflictException>(() =>
            _sut.AnswerQuestionAsync(sessionId, new ResultRequestDto { QuestionId = question.Id, UserAnswer = "oslo" }));
    }

    [Fact]
    public async Task FinishedSession_TakesNoAnswers()
    {
        var sessionId = Guid.NewGuid();
        _repo.Setup(x => x.GetSessionAsync(sessionId, _userId))
            .ReturnsAsync(new QuestionReviewSession { Id = sessionId, UserId = _userId, StartedAt = Now, FinishedAt = Now });

        await Should.ThrowAsync<ConflictException>(() =>
            _sut.AnswerQuestionAsync(sessionId, new ResultRequestDto { QuestionId = Guid.NewGuid() }));
    }
}

public class FlashcardTodaySessionTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IFlashcardRepository> _repo = new();
    private readonly FlashcardService _sut;

    public FlashcardTodaySessionTests()
    {
        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(_userId);

        _sut = new FlashcardService(
            _repo.Object,
            new FlashcardSchedulerProvider(
                [new BasicFlashcardScheduler()],
                new FlashcardSchedulerOptions { Algorithm = FlashcardAlgorithm.Basic }),
            new Mock<IFlashcardProgressRebuilder>().Object,
            user.Object);
    }

    private DictionaryEntry Entry(Guid dictionaryId) => new()
    {
        Id = Guid.NewGuid(),
        DictionaryId = dictionaryId,
        Term = "ord",
        Translation = "word",
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task DueWordsFromAllDictionaries_ThenNewUpToTheLimit()
    {
        var due = Enumerable.Range(0, 3).Select(i => Entry(Guid.NewGuid())).ToList();
        var fresh = Enumerable.Range(0, 2).Select(i => Entry(Guid.NewGuid())).ToList();
        var dueBy = DateTime.UtcNow.AddHours(5);
        _repo.Setup(x => x.GetDueEntriesAsync(_userId, dueBy, 10)).ReturnsAsync(due);
        _repo.Setup(x => x.GetNewEntriesAsync(_userId, 4)).ReturnsAsync(fresh);

        var session = await _sut.StartTodaySessionAsync(
            new FlashcardSessionCreateDto { Mode = FlashcardSessionMode.Due }, dueBy, newLimit: 4, maxCards: 10);

        session.DictionaryId.ShouldBeNull();
        session.Scope.ShouldBe(FlashcardSessionScope.Today);
        session.Cards.Count.ShouldBe(5);
        _repo.Verify(x => x.GetNewEntriesAsync(_userId, 4));
        _repo.Verify(x => x.AddSessionAsync(It.Is<FlashcardSession>(s => s.DictionaryId == null && s.Scope == FlashcardSessionScope.Today)));
    }

    [Fact]
    public async Task EmptyQueue_IsAConflict()
    {
        _repo.Setup(x => x.GetDueEntriesAsync(_userId, It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync([]);
        _repo.Setup(x => x.GetNewEntriesAsync(_userId, It.IsAny<int>())).ReturnsAsync([]);

        await Should.ThrowAsync<ConflictException>(() => _sut.StartTodaySessionAsync(
            new FlashcardSessionCreateDto { Mode = FlashcardSessionMode.Due }, DateTime.UtcNow, 20, 100));
    }
}
