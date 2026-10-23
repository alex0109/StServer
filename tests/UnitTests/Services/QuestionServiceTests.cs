using Application.Constants;
using Application.DTOs.Option;
using Application.DTOs.Question;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Question;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class QuestionServiceTests
{
    private readonly Mock<IQuestionRepository> _repo = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<IOptionService> _optionService = new();
    private readonly Mock<ILimitLock> _limitLock = new();
    private readonly Mock<ILimitLockScope> _limitLockScope = new();

    private readonly QuestionService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    
    public QuestionServiceTests()
    {
        _user.Setup(x => x.UserId)
            .Returns(_userId);

        _limitLock
            .Setup(x => x.AcquireAsync(It.IsAny<LimitScope>(), It.IsAny<Guid>()))
            .ReturnsAsync(_limitLockScope.Object);

        // Material belongs to the user unless a test says otherwise
        _repo
            .Setup(x => x.MaterialExistsAsync(It.IsAny<Guid>(), _userId))
            .ReturnsAsync(true);

        _sut = new QuestionService(
            _repo.Object,
            _user.Object,
            _optionService.Object,
            _limitLock.Object);
    }
    
    [Fact]
    public async Task GetActiveQuestionsAsync_ReturnsMappedQuestions()
    {
        var questions = new List<Question>
        {
            CreateQuestion("Question 1"),
            CreateQuestion("Question 2")
        };
        
        _repo.Setup(x =>
                x.GetActiveQuestionsAsync(
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync(questions);
        
        var result =
            await _sut.GetActiveQuestionsAsync(Guid.NewGuid());
        
        result.Count.ShouldBe(2);
        result[0].Title.ShouldBe("Question 1");
        result[1].Title.ShouldBe("Question 2");
    }

    [Fact]
    public async Task GetActiveReducedQuestionsAsync_ReturnsReducedDto()
    {
        var question = CreateQuestion("Test");
        
        _repo.Setup(x =>
                x.GetActiveQuestionsAsync(
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync([question]);
        
        var result =
            await _sut.GetActiveReducedQuestionsAsync(Guid.NewGuid());
        
        result.Count.ShouldBe(1);
        result[0].Title.ShouldBe("Test");
        result[0].Id.ShouldBe(question.Id);
    }
    
    [Fact]
    public async Task GetAllReducedQuestionsAsync_ReturnsAllQuestions()
    {
        _repo.Setup(x =>
                x.GetAllQuestionsAsync(
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync(
            [
                CreateQuestion("A"),
                CreateQuestion("B")
            ]);
        
        var result =
            await _sut.GetAllReducedQuestionsAsync(Guid.NewGuid());
        
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetByIdQuestionAsync_NotFound_ReturnsNull()
    {
        _repo.Setup(x =>
                x.GetByIdQuestionAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync((Question)null!);
        
        var result =
            await _sut.GetByIdQuestionAsync(
                Guid.NewGuid(),
                Guid.NewGuid());
        
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdQuestionAsync_Exists_ReturnsDto()
    {
        var question = CreateQuestion("Test");
        
        _repo.Setup(x =>
                x.GetByIdQuestionAsync(
                    question.MaterialId,
                    question.Id,
                    _userId))
            .ReturnsAsync(question);

        var result =
            await _sut.GetByIdQuestionAsync(
                question.MaterialId,
                question.Id);
        
        result.ShouldNotBeNull();
        result.Id.ShouldBe(question.Id);
        result.Title.ShouldBe("Test");
    }

    [Fact]
    public async Task CreateOpenQuestionAsync_CreatesQuestion()
    {
        var dto = new OpenQuestionCreateDto
        {
            Title = "Open",
            Answer = "Answer",
            QuestionDifficulty = QuestionDifficulty.Medium
        };
        
        _repo.Setup(x =>
                x.AddQuestionAsync(It.IsAny<Question>()))
            .ReturnsAsync((Question q) => q);

        var result =
            await _sut.CreateOpenQuestionAsync(
                Guid.NewGuid(),
                dto);
        
        result.ShouldNotBeNull();
        result.Title.ShouldBe("Open");
        
        _repo.Verify(x =>
            x.AddQuestionAsync(
                It.Is<Question>(q =>
                    q.UserId == _userId &&
                    q.Answer == "Answer" &&
                    q.QuestionType == QuestionType.Open)),
            Times.Once);
        
        _repo.Verify(x =>
            x.SaveChangesAsync(),
            Times.Once);
    }

    [Fact]
    public async Task CreateQuestionWithOptions_LimitReached_Throws()
    {
        _repo.Setup(x =>
                x.CountActiveByMaterialIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(UserLimits.MaxQuestionsPerMaterial);

        await Should.ThrowAsync<LimitExceededException>(
            () =>
                _sut.CreateQuestionWithOptionsAsync(
                    Guid.NewGuid(),
                    new OptionQuestionCreateDto
                    {
                        Title = "Test",
                        Options = []
                    }));
        
        _repo.Verify(x =>
            x.AddQuestionAsync(It.IsAny<Question>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateQuestionWithOptions_CreatesQuestionAndSyncsOptions()
    {
        _repo.Setup(x =>
                x.CountActiveByMaterialIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(0);

        _repo.Setup(x =>
                x.AddQuestionAsync(It.IsAny<Question>()))
            .ReturnsAsync((Question q)=>q);
        
        var dto = new OptionQuestionCreateDto
        {
            Title = "Options",
            Options =
            [
                new OptionCreateDto
                {
                    Name = "A",
                    IsCorrect = true
                }
            ]
        };
        
        var result =
            await _sut.CreateQuestionWithOptionsAsync(
                Guid.NewGuid(),
                dto);

        result.Title.ShouldBe("Options");
        
        _optionService.Verify(x =>
            x.SyncOptions(
                It.IsAny<Question>(),
                It.Is<List<OptionUpdateDto>>(o =>
                    o.Count == 1 && o[0].Name == "A" && o[0].IsCorrect == true && o[0].Id == null)),
            Times.Once);
        
        _repo.Verify(x =>
            x.SaveChangesAsync(),
            Times.Once);
    }
    
    [Fact]
    public async Task UpdateQuestionAsync_NotFound_ReturnsNull()
    {
        _repo.Setup(x =>
                x.GetByIdQuestionAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync((Question)null!);

        var result =
            await _sut.UpdateQuestionAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new QuestionUpdateDto
                {
                    Options = null
                });
        
        result.ShouldBeNull();
        
        _repo.Verify(x =>
            x.SaveChangesAsync(),
            Times.Never);
    }

    [Fact]
    public async Task UpdateQuestionAsync_UpdatesQuestion()
    {
        var question = CreateQuestion("Old");

        _repo.Setup(x =>
                x.GetByIdQuestionAsync(
                    question.MaterialId,
                    question.Id,
                    _userId))
            .ReturnsAsync(question);

        var result =
            await _sut.UpdateQuestionAsync(
                question.MaterialId,
                question.Id,
                new QuestionUpdateDto
                {
                    Title = "New",
                    Options = null
                });
        
        result.ShouldNotBeNull();

        result.Title.ShouldBe("New");
        
        _repo.Verify(x =>
            x.SaveChangesAsync(),
            Times.Once);
    }

    [Fact]
    public async Task UpdateQuestionAsync_WithOptions_SyncsOptions()
    {
        var question = CreateQuestion("Test");
        question.QuestionType = QuestionType.Options;
        question.Answer = null;
        
        _repo.Setup(x =>
                x.GetByIdQuestionAsync(
                    question.MaterialId,
                    question.Id,
                    _userId))
            .ReturnsAsync(question);
        
        var options = new List<OptionUpdateDto>();
        
        await _sut.UpdateQuestionAsync(
            question.MaterialId,
            question.Id,
            new QuestionUpdateDto
            {
                Options = options
            });
        
        _optionService.Verify(x =>
            x.SyncOptions(
                question,
                options),
            Times.Once);
    }
    
    [Fact]
    public async Task DeleteQuestionAsync_NotFound_ReturnsFalse()
    {
        _repo.Setup(x =>
                x.GetByIdQuestionAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    _userId))
            .ReturnsAsync((Question)null!);
        
        var result =
            await _sut.DeleteQuestionAsync(
                Guid.NewGuid(),
                Guid.NewGuid());
        
        result.ShouldBeFalse();

        _repo.Verify(x =>
            x.SaveChangesAsync(),
            Times.Never);
    }

    [Fact]
    public async Task DeleteQuestionAsync_Exists_SoftDeletes()
    {
        var question = CreateQuestion("Delete");
        
        _repo.Setup(x =>
                x.GetByIdQuestionAsync(
                    question.MaterialId,
                    question.Id,
                    _userId))
            .ReturnsAsync(question);

        var result =
            await _sut.DeleteQuestionAsync(
                question.MaterialId,
                question.Id);
        
        result.ShouldBeTrue();

        question.IsActive.ShouldBeFalse();
        question.DeletedAt.ShouldNotBeNull();

        _repo.Verify(x =>
            x.SaveChangesAsync(),
            Times.Once);
    }

    [Fact]
    public async Task CreateTrueFalseQuestionAsync_CreatesQuestionWithFixedOptions()
    {
        _repo.Setup(x =>
                x.CountActiveByMaterialIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(0);

        var result = await _sut.CreateTrueFalseQuestionAsync(
            Guid.NewGuid(),
            new TrueFalseQuestionCreateDto
            {
                Title = "The Earth is round",
                IsTrue = true,
                QuestionDifficulty = QuestionDifficulty.Easy
            });

        result.QuestionType.ShouldBe(QuestionType.TrueFalse);

        _optionService.Verify(x =>
            x.SetTrueFalseOptions(
                It.Is<Question>(q => q.QuestionType == QuestionType.TrueFalse),
                true),
            Times.Once);

        _repo.Verify(x => x.AddQuestionAsync(It.IsAny<Question>()), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateTrueFalseQuestionAsync_LimitReached_Throws()
    {
        _repo.Setup(x =>
                x.CountActiveByMaterialIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(UserLimits.MaxQuestionsPerMaterial);

        await Should.ThrowAsync<LimitExceededException>(() =>
            _sut.CreateTrueFalseQuestionAsync(
                Guid.NewGuid(),
                new TrueFalseQuestionCreateDto { Title = "Test", IsTrue = false }));
    }

    [Fact]
    public async Task UpdateQuestionAsync_OpenQuestionWithOptions_ThrowsBadRequest()
    {
        var question = CreateQuestion("Open");

        _repo.Setup(x =>
                x.GetByIdQuestionAsync(question.MaterialId, question.Id, _userId))
            .ReturnsAsync(question);

        await Should.ThrowAsync<BadRequestException>(() =>
            _sut.UpdateQuestionAsync(
                question.MaterialId,
                question.Id,
                new QuestionUpdateDto { Options = [] }));

        _repo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateQuestionAsync_TrueFalseWithOptions_ThrowsBadRequest()
    {
        var question = CreateQuestion("True/False");
        question.QuestionType = QuestionType.TrueFalse;
        question.Answer = null;

        _repo.Setup(x =>
                x.GetByIdQuestionAsync(question.MaterialId, question.Id, _userId))
            .ReturnsAsync(question);

        await Should.ThrowAsync<BadRequestException>(() =>
            _sut.UpdateQuestionAsync(
                question.MaterialId,
                question.Id,
                new QuestionUpdateDto { Options = [] }));
    }

    [Fact]
    public async Task UpdateQuestionAsync_TrueFalseCorrectOption_SetsItThroughOptionService()
    {
        var question = CreateQuestion("True/False");
        question.QuestionType = QuestionType.TrueFalse;
        question.Answer = null;
        var optionId = Guid.NewGuid();

        _repo.Setup(x =>
                x.GetByIdQuestionAsync(question.MaterialId, question.Id, _userId))
            .ReturnsAsync(question);

        await _sut.UpdateQuestionAsync(
            question.MaterialId,
            question.Id,
            new QuestionUpdateDto { CorrectOptionId = optionId });

        _optionService.Verify(x => x.SetCorrectOption(question, optionId), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateQuestionAsync_OptionsQuestionWithTextAnswer_ThrowsBadRequest()
    {
        var question = CreateQuestion("Options");
        question.QuestionType = QuestionType.Options;
        question.Answer = null;

        _repo.Setup(x =>
                x.GetByIdQuestionAsync(question.MaterialId, question.Id, _userId))
            .ReturnsAsync(question);

        await Should.ThrowAsync<BadRequestException>(() =>
            _sut.UpdateQuestionAsync(
                question.MaterialId,
                question.Id,
                new QuestionUpdateDto { Answer = "text" }));
    }

    [Fact]
    public async Task CreateOpenQuestionAsync_ForeignMaterial_ReturnsNullWithoutLocking()
    {
        var materialId = Guid.NewGuid();
        _repo.Setup(x => x.MaterialExistsAsync(materialId, _userId)).ReturnsAsync(false);

        var result = await _sut.CreateOpenQuestionAsync(materialId,
            new OpenQuestionCreateDto { Title = "Q", Answer = "A" });

        result.ShouldBeNull();
        _limitLock.Verify(x => x.AcquireAsync(It.IsAny<LimitScope>(), It.IsAny<Guid>()), Times.Never);
        _repo.Verify(x => x.AddQuestionAsync(It.IsAny<Question>()), Times.Never);
    }

    [Fact]
    public async Task CreateOpenQuestionAsync_CountsUnderMaterialLockAndCommits()
    {
        var materialId = Guid.NewGuid();
        _repo.Setup(x => x.CountActiveByMaterialIdAsync(materialId)).ReturnsAsync(UserLimits.MaxQuestionsPerMaterial - 1);

        var result = await _sut.CreateOpenQuestionAsync(materialId,
            new OpenQuestionCreateDto { Title = "Q", Answer = "A" });

        result.ShouldNotBeNull();
        _limitLock.Verify(x => x.AcquireAsync(LimitScope.Questions, materialId), Times.Once);
        _limitLockScope.Verify(x => x.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateTrueFalseQuestionAsync_LimitReached_DoesNotCommit()
    {
        _repo.Setup(x => x.CountActiveByMaterialIdAsync(It.IsAny<Guid>())).ReturnsAsync(UserLimits.MaxQuestionsPerMaterial);

        await Should.ThrowAsync<LimitExceededException>(() =>
            _sut.CreateTrueFalseQuestionAsync(Guid.NewGuid(),
                new TrueFalseQuestionCreateDto { Title = "Q", IsTrue = true }));

        _limitLockScope.Verify(x => x.CommitAsync(), Times.Never);
        _optionService.Verify(x => x.SetTrueFalseOptions(It.IsAny<Question>(), It.IsAny<bool>()), Times.Never);
    }

    private Question CreateQuestion(string title)
    {
        return new Question
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            UserId = _userId,
            Title = title,
            Answer = "Answer",
            QuestionType = QuestionType.Open,
            QuestionDifficulty = QuestionDifficulty.Easy,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}