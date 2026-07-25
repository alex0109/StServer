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

    private readonly QuestionService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    
    public QuestionServiceTests()
    {
        _user.Setup(x => x.UserId)
            .Returns(_userId);

        _sut = new QuestionService(
            _repo.Object,
            _user.Object,
            _optionService.Object);
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
                x.CountByMaterialIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(UserLimits.MaxQuestionsPerMaterial);

        await Should.ThrowAsync<LimitExceededException>(
            () =>
                _sut.CreateQuestionWithOptionsAsync(
                    Guid.NewGuid(),
                    new OptionQuestionCreateDto
                    {
                        CorrectOptionId =  Guid.NewGuid(),
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
                x.CountByMaterialIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(0);

        _repo.Setup(x =>
                x.AddQuestionAsync(It.IsAny<Question>()))
            .ReturnsAsync((Question q)=>q);
        
        var dto = new OptionQuestionCreateDto
        {
            CorrectOptionId =  Guid.NewGuid(),
            Title = "Options",
            Options =
            [
                new OptionUpdateDto
                {
                    Name = "A"
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
                dto.Options),
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