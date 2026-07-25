using Application.DTOs.Attempt;
using Application.DTOs.Question;
using Application.DTOs.Result;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Attempt;
using Domain.Utility.Question;
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
            _scoring.Object);
    }
    
    [Fact]
    public async Task GetFinishedAttempts_NoAttempts_ReturnsNull()
    {
        _repo.Setup(x =>
                x.GetFinishedAttemptsAsync(It.IsAny<Guid>(), _userId))
            .ReturnsAsync([]);

        var result = await _sut.GetFinishedAttempts(Guid.NewGuid());

        result.ShouldBeNull();
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
                CorrectOptionId = optionId,
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
                    UserAnswerOptionId = optionId
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
                CorrectOptionId = Guid.NewGuid(),
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
                    UserAnswerOptionId = Guid.NewGuid()
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
                QuestionDifficulty = QuestionDifficulty.Medium,
                Title = "Test",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            });


        _evaluation.Setup(x =>
                x.EvaluateAnswer("correct","answer"))
            .Returns(new AnswerEvaluationResult
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