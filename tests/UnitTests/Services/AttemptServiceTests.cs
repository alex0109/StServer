using Application.DTOs.Attempt;
using Application.DTOs.Question;
using Application.DTOs.Result;
using Application.Interfaces;
using Application.Evaluators;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Attempt;
using Domain.Utility.Question;
using Domain.Utility.Result;
using Domain.Utility.Review;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class AttemptServiceTests
{
    private readonly Mock<IAttemptRepository> _repo = new();
    private readonly Mock<IQuestionRepository> _questionRepo = new();
    private readonly Mock<IAssessmentRepository> _assessmentRepo = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<IAnswerEvaluationService> _evaluation = new();
    private readonly Mock<IAttemptScoringService> _scoring = new();

    private readonly AttemptService _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public AttemptServiceTests()
    {
        _user.Setup(x => x.UserId)
            .Returns(_userId);

        _sut = new AttemptService(
            _repo.Object,
            _questionRepo.Object,
            _assessmentRepo.Object,
            _user.Object,
            _evaluation.Object,
            _scoring.Object,
            new ConfidenceReviewRatingResolver());

        // Option grading has no dependencies, so the real implementation is used
        _evaluation.Setup(x =>
                x.EvaluateOptions(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<IReadOnlyCollection<Guid>?>()))
            .Returns((IReadOnlyCollection<Guid> correct, IReadOnlyCollection<Guid>? chosen) =>
                new AnswerEvaluationService(Mock.Of<IAnswerNormalizer>(), []).EvaluateOptions(correct, chosen));
    }
    
    [Fact]
    public async Task GetFinishedAttempts_NoAttempts_ReturnsEmpty()
    {
        _repo.Setup(x =>
                x.GetFinishedAttemptsAsync(It.IsAny<Guid>(), _userId))
            .ReturnsAsync([]);

        var result = await _sut.GetFinishedAttempts(Guid.NewGuid());

        result.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task GetFinishedAttempts_ReturnsMappedAttempts()
    {
        var attempts = new List<Attempt>
        {
            new()
            {
                Id = Guid.NewGuid(),
                AssessmentId = Guid.NewGuid(),
                AttemptStatus = AttemptStatus.Finished,
                StartedAt = DateTime.UtcNow
            }
        };


        _repo.Setup(x =>
                x.GetFinishedAttemptsAsync(It.IsAny<Guid>(), _userId))
            .ReturnsAsync(attempts);


        var result = await _sut.GetFinishedAttempts(Guid.NewGuid());


        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);

        _scoring.Verify(x =>
            x.AverageScoreAttempt(
                It.IsAny<AttemptResponseDto>()),
            Times.Once);
    }
    
    [Fact]
    public async Task GetAttempt_NotFound_ReturnsNull()
    {
        _repo.Setup(x =>
                x.GetAttemptWithResultsByIdAsync(
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync((Attempt)null!);


        var result =
            await _sut.GetAttempt(Guid.NewGuid());


        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAttempt_Exists_ReturnsDto()
    {
        var attempt = CreateAttempt();


        _repo.Setup(x =>
                x.GetAttemptWithResultsByIdAsync(
                    attempt.Id,
                    _userId))
            .ReturnsAsync(attempt);


        var result =
            await _sut.GetAttempt(attempt.Id);


        result.ShouldNotBeNull();
        result.Id.ShouldBe(attempt.Id);

        _scoring.Verify(x =>
            x.AverageScoreAttempt(result),
            Times.Once);
    }

    [Fact]
    public async Task StartAttempt_AssessmentNotFound_ReturnsNull()
    {
        _assessmentRepo.Setup(x =>
                x.GetAssessmentByMaterialIdAsync(
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync((Assessment)null!);


        var result =
            await _sut.StartAttempt(Guid.NewGuid());


        result.ShouldBeNull();

        _repo.Verify(x =>
            x.AddAttemptAsync(It.IsAny<Attempt>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAttempt_AssessmentExists_CreatesAttempt()
    {
        var assessmentId = Guid.NewGuid();


        _assessmentRepo.Setup(x =>
                x.GetAssessmentByMaterialIdAsync(
                    assessmentId,
                    _userId))
            .ReturnsAsync(new Assessment
            {
                Id = assessmentId,
                UserId = _userId
            });


        var result =
            await _sut.StartAttempt(assessmentId);


        result.ShouldNotBe(Guid.Empty);


        _repo.Verify(x =>
            x.AddAttemptAsync(
                It.Is<Attempt>(a =>
                    a.AssessmentId == assessmentId &&
                    a.UserId == _userId &&
                    a.AttemptStatus == AttemptStatus.InProgress)),
            Times.Once);


        _repo.Verify(x =>
            x.SaveChangesAsync(),
            Times.Once);
    }
    
    [Fact]
    public async Task AnswerQuestion_AttemptNotFound_ReturnsFalseResult()
    {
        _repo.Setup(x =>
                x.GetFullAttemptByIdAsync(
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync((Attempt)null!);


        var result =
            await _sut.AnswerQuestion(
                Guid.NewGuid(),
                new ResultRequestDto());


        result.IsCorrect.ShouldBeFalse();
        result.Score.ShouldBe(0);
        result.Method.ShouldBe(EvaluationMethod.None);
    }
    
    [Fact]
    public async Task AnswerQuestion_CorrectOption_ReturnsExact()
    {
        var attempt = CreateAttempt();

        var questionId = Guid.NewGuid();
        var optionId = Guid.NewGuid();


        _repo.Setup(x =>
                x.GetFullAttemptByIdAsync(
                    attempt.Id,
                    _userId))
            .ReturnsAsync(attempt);


        _questionRepo.Setup(x =>
                x.GetByIdQuestionAsync(
                    attempt.Assessment.MaterialId,
                    questionId,
                    _userId))
            .ReturnsAsync(new Question
            {
                Id = questionId,
                Options = [new Option { Id = optionId, Name = "Correct", IsCorrect = true }],
                QuestionDifficulty = QuestionDifficulty.Easy,
                Title = "Test",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            });

        var result =
            await _sut.AnswerQuestion(
                attempt.Id,
                new ResultRequestDto
                {
                    QuestionId = questionId,
                    UserAnswerOptionIds = [optionId]
                });

        result.IsCorrect.ShouldBeTrue();
        result.Method.ShouldBe(EvaluationMethod.Exact);
        result.Score.ShouldBe(100);

        _repo.Verify(x =>
            x.AddResultAsync(It.IsAny<Result>()),
            Times.Once);
    }

    [Fact]
    public async Task AnswerQuestion_WrongOption_ReturnsFalse()
    {
        var attempt = CreateAttempt();

        var questionId = Guid.NewGuid();


        _repo.Setup(x =>
                x.GetFullAttemptByIdAsync(
                    attempt.Id,
                    _userId))
            .ReturnsAsync(attempt);


        _questionRepo.Setup(x =>
                x.GetByIdQuestionAsync(
                    It.IsAny<Guid>(),
                    questionId,
                    _userId))
            .ReturnsAsync(new Question
            {
                Id = questionId,
                Options = [new Option { Id = Guid.NewGuid(), Name = "Correct", IsCorrect = true }],
                QuestionDifficulty = QuestionDifficulty.Easy,
                Title = "Test",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            });


        var result =
            await _sut.AnswerQuestion(
                attempt.Id,
                new ResultRequestDto
                {
                    QuestionId = questionId,
                    UserAnswerOptionIds = [Guid.NewGuid()]
                });


        result.IsCorrect.ShouldBeFalse();
        result.Method.ShouldBe(EvaluationMethod.Exact);
    }
    
    [Fact]
    public async Task AnswerQuestion_OpenQuestion_UsesEvaluator()
    {
        var attempt = CreateAttempt();

        var questionId = Guid.NewGuid();


        _repo.Setup(x =>
                x.GetFullAttemptByIdAsync(
                    attempt.Id,
                    _userId))
            .ReturnsAsync(attempt);


        _questionRepo.Setup(x =>
                x.GetByIdQuestionAsync(
                    It.IsAny<Guid>(),
                    questionId,
                    _userId))
            .ReturnsAsync(new Question
            {
                Id = questionId,
                Answer = "correct",
                QuestionType = QuestionType.Open,
                QuestionDifficulty = QuestionDifficulty.Medium,
                Title = "Test",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            });


        _evaluation.Setup(x =>
                x.EvaluateAnswerAsync("Test", "correct", "answer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerEvaluationResult
            {
                IsCorrect = true,
                Score = 90,
                Method = EvaluationMethod.Fuzzy
            });



        var result =
            await _sut.AnswerQuestion(
                attempt.Id,
                new ResultRequestDto
                {
                    QuestionId = questionId,
                    UserAnswer = "answer"
                });



        result.Method.ShouldBe(EvaluationMethod.Fuzzy);
        result.Score.ShouldBe(90);
    }
    
    [Fact]
    public async Task FinishAttempt_NotFound_ReturnsFalse()
    {
        _repo.Setup(x =>
                x.GetAttemptWithResultsByIdAsync(
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync((Attempt)null!);


        var result =
            await _sut.FinishAttempt(Guid.NewGuid());


        result.ShouldBeFalse();
    }

    [Fact]
    public async Task FinishAttempt_Exists_FinishesAttempt()
    {
        var attempt = CreateAttempt();


        _repo.Setup(x =>
                x.GetAttemptWithResultsByIdAsync(
                    attempt.Id,
                    _userId))
            .ReturnsAsync(attempt);


        var result =
            await _sut.FinishAttempt(attempt.Id);


        result.ShouldBeTrue();

        attempt.AttemptStatus
            .ShouldBe(AttemptStatus.Finished);


        attempt.FinishedAt.ShouldNotBeNull();


        _repo.Verify(x =>
            x.SaveChangesAsync(),
            Times.Once);
    }

    [Fact]
    public async Task MarkAbandonedAttempts_NoAttempts_DoesNothing()
    {
        _repo.Setup(x =>
                x.GetAbandonedAttemptsAsync())
            .ReturnsAsync([]);


        await _sut.MarkAbandonedAttempts();


        _repo.Verify(x =>
            x.SaveChangesAsync(),
            Times.Never);
    }

    [Fact]
    public async Task MarkAbandonedAttempts_ChangesStatus()
    {
        var attempts = new List<Attempt>
        {
            CreateAttempt()
        };


        _repo.Setup(x =>
                x.GetAbandonedAttemptsAsync())
            .ReturnsAsync(attempts);


        await _sut.MarkAbandonedAttempts();


        attempts[0]
            .AttemptStatus
            .ShouldBe(AttemptStatus.Abandoned);


        _repo.Verify(x =>
            x.SaveChangesAsync(),
            Times.Once);
    }
    
    [Fact]
    public async Task GetResults_NotFound_ReturnsNull()
    {
        _repo.Setup(x =>
                x.GetFullAttemptByIdAsync(
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync((Attempt)null!);


        var result =
            await _sut.GetResults(Guid.NewGuid());


        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResults_ReturnsResult()
    {
        var attempt = CreateAttempt();


        _repo.Setup(x =>
                x.GetFullAttemptByIdAsync(
                    attempt.Id,
                    _userId))
            .ReturnsAsync(attempt);


        var result =
            await _sut.GetResults(attempt.Id);


        result.ShouldNotBeNull();

        _scoring.Verify(x =>
            x.AverageScoreAttempt(
                It.IsAny<AttemptResponseDto>()),
            Times.Once);
    }
    
    [Fact]
    public async Task AnswerQuestion_WrongOption_ScoresZeroAndRatesAgain()
    {
        var attempt = CreateAttempt();
        var questionId = Guid.NewGuid();
        Result? saved = null;

        _repo.Setup(x => x.GetFullAttemptByIdAsync(attempt.Id, _userId)).ReturnsAsync(attempt);
        _repo.Setup(x => x.AddResultAsync(It.IsAny<Result>())).Callback<Result>(r => saved = r);
        _questionRepo.Setup(x =>
                x.GetByIdQuestionAsync(It.IsAny<Guid>(), questionId, _userId))
            .ReturnsAsync(new Question
            {
                Id = questionId,
                QuestionType = QuestionType.Options,
                Options = [new Option { Id = Guid.NewGuid(), Name = "Correct", IsCorrect = true }],
                QuestionDifficulty = QuestionDifficulty.Easy,
                Title = "Test",
                CreatedAt = DateTime.UtcNow
            });

        var result = await _sut.AnswerQuestion(attempt.Id, new ResultRequestDto
        {
            QuestionId = questionId,
            UserAnswerOptionIds = [Guid.NewGuid()],
            ConfidenceLevel = ConfidenceLevel.High
        });

        result.IsCorrect.ShouldBeFalse();
        result.Score.ShouldBe(0);
        saved.ShouldNotBeNull();
        saved.Rating.ShouldBe(ReviewRating.Again);
    }

    [Fact]
    public async Task AnswerQuestion_TrueFalseCorrect_IsGradedByOptionAndRated()
    {
        var attempt = CreateAttempt();
        var questionId = Guid.NewGuid();
        var trueOptionId = Guid.NewGuid();
        Result? saved = null;

        _repo.Setup(x => x.GetFullAttemptByIdAsync(attempt.Id, _userId)).ReturnsAsync(attempt);
        _repo.Setup(x => x.AddResultAsync(It.IsAny<Result>())).Callback<Result>(r => saved = r);
        _questionRepo.Setup(x =>
                x.GetByIdQuestionAsync(It.IsAny<Guid>(), questionId, _userId))
            .ReturnsAsync(new Question
            {
                Id = questionId,
                QuestionType = QuestionType.TrueFalse,
                Options = [new Option { Id = trueOptionId, Name = "Correct", IsCorrect = true }],
                QuestionDifficulty = QuestionDifficulty.Easy,
                Title = "The Earth is round",
                CreatedAt = DateTime.UtcNow
            });

        var result = await _sut.AnswerQuestion(attempt.Id, new ResultRequestDto
        {
            QuestionId = questionId,
            UserAnswerOptionIds = [trueOptionId],
            ConfidenceLevel = ConfidenceLevel.Low
        });

        result.IsCorrect.ShouldBeTrue();
        result.Score.ShouldBe(100);
        saved.ShouldNotBeNull();
        saved.UserAnswerOptionIds.ShouldBe([trueOptionId]);
        saved.Rating.ShouldBe(ReviewRating.Hard);
        _evaluation.Verify(x => x.EvaluateAnswer(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AnswerQuestion_ChangedAnswer_UpdatesRating()
    {
        var attempt = CreateAttempt();
        var questionId = Guid.NewGuid();
        var correctOptionId = Guid.NewGuid();
        var existing = new Result
        {
            Id = Guid.NewGuid(),
            AttemptId = attempt.Id,
            QuestionId = questionId,
            UserAnswerOptionIds = [Guid.NewGuid()],
            IsCorrect = false,
            Score = 0,
            Weight = 1,
            Rating = ReviewRating.Again,
            AnsweredAt = DateTime.UtcNow.AddMinutes(-1)
        };
        attempt.Results.Add(existing);

        _repo.Setup(x => x.GetFullAttemptByIdAsync(attempt.Id, _userId)).ReturnsAsync(attempt);
        _questionRepo.Setup(x =>
                x.GetByIdQuestionAsync(It.IsAny<Guid>(), questionId, _userId))
            .ReturnsAsync(new Question
            {
                Id = questionId,
                QuestionType = QuestionType.Options,
                Options = [new Option { Id = correctOptionId, Name = "Correct", IsCorrect = true }],
                QuestionDifficulty = QuestionDifficulty.Easy,
                Title = "Test",
                CreatedAt = DateTime.UtcNow
            });

        await _sut.AnswerQuestion(attempt.Id, new ResultRequestDto
        {
            QuestionId = questionId,
            UserAnswerOptionIds = [correctOptionId]
        });

        existing.IsCorrect.ShouldBeTrue();
        existing.Rating.ShouldBe(ReviewRating.Good);
        _repo.Verify(x => x.AddResultAsync(It.IsAny<Result>()), Times.Never);
    }

    [Fact]
    public async Task AnswerQuestion_SeveralCorrectOptions_NeedsAllOfThem()
    {
        var attempt = CreateAttempt();
        var questionId = Guid.NewGuid();
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        Result? saved = null;

        _repo.Setup(x => x.GetFullAttemptByIdAsync(attempt.Id, _userId)).ReturnsAsync(attempt);
        _repo.Setup(x => x.AddResultAsync(It.IsAny<Result>())).Callback<Result>(r => saved = r);
        _questionRepo.Setup(x =>
                x.GetByIdQuestionAsync(It.IsAny<Guid>(), questionId, _userId))
            .ReturnsAsync(new Question
            {
                Id = questionId,
                QuestionType = QuestionType.Options,
                QuestionDifficulty = QuestionDifficulty.Easy,
                Title = "Pick prime numbers",
                CreatedAt = DateTime.UtcNow,
                Options =
                [
                    new Option { Id = a, Name = "2", IsCorrect = true },
                    new Option { Id = b, Name = "3", IsCorrect = true },
                    new Option { Id = c, Name = "4" }
                ]
            });

        var result = await _sut.AnswerQuestion(attempt.Id, new ResultRequestDto
        {
            QuestionId = questionId,
            UserAnswerOptionIds = [a],
            ConfidenceLevel = ConfidenceLevel.High
        });

        result.IsCorrect.ShouldBeFalse();
        result.Score.ShouldBe(50);
        saved.ShouldNotBeNull();
        saved.UserAnswerOptionIds.ShouldBe([a]);
        saved.Rating.ShouldBe(ReviewRating.Again);
    }

    private Attempt CreateAttempt()
    {
        return new Attempt
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            AssessmentId = Guid.NewGuid(),
            Assessment = new Assessment
            {
                MaterialId = Guid.NewGuid()
            },
            AttemptStatus = AttemptStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };
    }
}