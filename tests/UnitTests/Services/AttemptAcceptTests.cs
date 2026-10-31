using Application.Evaluators;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Attempt;
using Domain.Utility.Question;
using Domain.Utility.Result;
using Domain.Utility.Review;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class AttemptAcceptTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IAttemptRepository> _repo = new();
    private readonly Mock<IQuestionReviewService> _reviews = new();
    private readonly AttemptService _sut;

    public AttemptAcceptTests()
    {
        var user = new Mock<IUserContext>();
        user.Setup(x => x.UserId).Returns(_userId);

        _sut = new AttemptService(
            _repo.Object,
            new Mock<IQuestionRepository>().Object,
            new Mock<IAssessmentRepository>().Object,
            user.Object,
            new Mock<IAnswerEvaluationService>().Object,
            new Mock<IAttemptScoringService>().Object,
            new ConfidenceReviewRatingResolver(),
            _reviews.Object);
    }

    private (Attempt Attempt, Result Result) Setup(
        EvaluationMethod method = EvaluationMethod.Fuzzy,
        AttemptStatus status = AttemptStatus.Finished,
        bool isCorrect = false)
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = "Name the process",
            Answer = "Photosynthesis",
            QuestionType = QuestionType.Open,
            CreatedAt = DateTime.UtcNow,
        };
        var attempt = new Attempt
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            AttemptStatus = status,
            StartedAt = DateTime.UtcNow.AddMinutes(-20),
            Assessment = new Assessment { Id = Guid.NewGuid(), UserId = _userId, MaterialId = Guid.NewGuid() },
        };
        var result = new Result
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            AttemptId = attempt.Id,
            QuestionId = question.Id,
            Question = question,
            IsCorrect = isCorrect,
            Score = 62,
            Weight = 1,
            Method = method,
            Rating = isCorrect ? ReviewRating.Good : ReviewRating.Again,
            AiExplanation = "You mixed it up with respiration",
            AnsweredAt = DateTime.UtcNow.AddMinutes(-15),
        };
        attempt.Results.Add(result);

        _repo.Setup(x => x.GetFullAttemptByIdAsync(attempt.Id, _userId)).ReturnsAsync(attempt);
        return (attempt, result);
    }

    [Fact]
    public async Task Accept_MakesTheAnswerRightAndReRatesTheReview()
    {
        var (attempt, result) = Setup();

        var dto = await _sut.AcceptAnswerAsync(attempt.Id, result.Id);

        dto.ShouldNotBeNull();
        result.IsCorrect.ShouldBeTrue();
        result.Score.ShouldBe(100);
        result.Method.ShouldBe(EvaluationMethod.Manual);
        result.AiExplanation.ShouldBeNull();
        result.OriginalScore.ShouldBe(62);
        result.OriginalMethod.ShouldBe(EvaluationMethod.Fuzzy);
        result.Rating.ShouldBe(ReviewRating.Good);
        _reviews.Verify(x => x.ReRateAttemptAnswerAsync(attempt.Id, result.QuestionId, _userId, ReviewRating.Good), Times.Once);
    }

    [Fact]
    public async Task AcceptTwice_ChangesNothingTheSecondTime()
    {
        var (attempt, result) = Setup();

        await _sut.AcceptAnswerAsync(attempt.Id, result.Id);
        await _sut.AcceptAnswerAsync(attempt.Id, result.Id);

        result.OriginalMethod.ShouldBe(EvaluationMethod.Fuzzy);
        _reviews.Verify(x => x.ReRateAttemptAnswerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ReviewRating>()), Times.Once);
    }

    [Fact]
    public async Task SomeoneElsesAttempt_IsNotFound()
    {
        (await _sut.AcceptAnswerAsync(Guid.NewGuid(), Guid.NewGuid())).ShouldBeNull();
    }

    [Fact]
    public async Task UnfinishedAttempt_CannotBeAccepted()
    {
        var (attempt, result) = Setup(status: AttemptStatus.InProgress);

        await Should.ThrowAsync<BadRequestException>(() => _sut.AcceptAnswerAsync(attempt.Id, result.Id));
    }

    [Fact]
    public async Task ExactCheck_CannotBeAccepted()
    {
        var (attempt, result) = Setup(method: EvaluationMethod.Exact);

        await Should.ThrowAsync<BadRequestException>(() => _sut.AcceptAnswerAsync(attempt.Id, result.Id));
    }

    [Fact]
    public async Task Undo_RestoresTheOriginalGrade()
    {
        var (attempt, result) = Setup(method: EvaluationMethod.AI);
        await _sut.AcceptAnswerAsync(attempt.Id, result.Id);

        await _sut.UndoAcceptAsync(attempt.Id, result.Id);

        result.IsCorrect.ShouldBeFalse();
        result.Score.ShouldBe(62);
        result.Method.ShouldBe(EvaluationMethod.AI);
        result.OverriddenAt.ShouldBeNull();
        result.Rating.ShouldBe(ReviewRating.Again);
    }

    [Fact]
    public async Task Undo_IsLimitedInTime()
    {
        var (attempt, result) = Setup();
        await _sut.AcceptAnswerAsync(attempt.Id, result.Id);
        result.OverriddenAt = DateTime.UtcNow - AttemptService.AcceptUndoWindow - TimeSpan.FromMinutes(1);

        await Should.ThrowAsync<ConflictException>(() => _sut.UndoAcceptAsync(attempt.Id, result.Id));
    }
}
