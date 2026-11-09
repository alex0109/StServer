using Application.DTOs.Assessment;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Question;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class AssessmentServiceTests
{
    private readonly Mock<IAssessmentRepository> _repo = new();
    private readonly Mock<IUserContext> _user = new();

    private readonly AssessmentService _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public AssessmentServiceTests()
    {
        _user
            .Setup(x => x.UserId)
            .Returns(_userId);

        _sut = new AssessmentService(
            _repo.Object,
            _user.Object);
    }

    [Fact]
    public async Task StartAssessment_ExistingAssessment_ReturnsExistingId()
    {
        var materialId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();

        var existing = new Assessment
        {
            Id = assessmentId,
            MaterialId = materialId,
            UserId = _userId
        };
        
        _repo
            .Setup(x => x.GetAssessmentByMaterialIdAsync(
                materialId,
                _userId))
            .ReturnsAsync(existing);
        
        var result = await _sut.StartAssessment(materialId);
        
        result.ShouldBe(assessmentId);
        
        _repo.Verify(
            x => x.AddAssessmentAsync(It.IsAny<Assessment>()),
            Times.Never);
        
        _repo.Verify(
            x => x.SaveChangesAsync(),
            Times.Never);
    }
    
    [Fact]
    public async Task StartAssessment_NoExistingAssessment_CreatesNew()
    {
        var materialId = Guid.NewGuid();
        
        _repo
            .Setup(x => x.GetAssessmentByMaterialIdAsync(
                materialId,
                _userId))
            .ReturnsAsync((Assessment?)null);
        
        var result = await _sut.StartAssessment(materialId);
        
        result.ShouldNotBe(Guid.Empty);

        _repo.Verify(
            x => x.AddAssessmentAsync(
                It.Is<Assessment>(a =>
                    a.MaterialId == materialId &&
                    a.UserId == _userId)),
            Times.Once);
        
        _repo.Verify(
            x => x.SaveChangesAsync(),
            Times.Once);
    }

    [Fact]
    public async Task StartAssessment_ShouldCreateUniqueId()
    {
        var materialId = Guid.NewGuid();
        
        _repo
            .Setup(x => x.GetAssessmentByMaterialIdAsync(
                It.IsAny<Guid>(),
                _userId))
            .ReturnsAsync((Assessment?)null);
        
        var result = await _sut.StartAssessment(materialId);
        
        result.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task StartAssessment_ShouldUseCurrentUser()
    {
        var materialId = Guid.NewGuid();
        
        _repo
            .Setup(x => x.GetAssessmentByMaterialIdAsync(
                materialId,
                _userId))
            .ReturnsAsync((Assessment?)null);

        await _sut.StartAssessment(materialId);

        _repo.Verify(
            x => x.AddAssessmentAsync(
                It.Is<Assessment>(a =>
                    a.UserId == _userId)),
            Times.Once);
    }
    
    [Fact]
    public async Task GetAssessment_AssessmentExists_ReturnsDto()
    {
        var assessmentId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        
        var assessment = new Assessment
        {
            Id = assessmentId,
            MaterialId = materialId,
            UserId = _userId
        };

        _repo
            .Setup(x => x.GetAssessmentByMaterialIdAsync(
                assessmentId,
                _userId))
            .ReturnsAsync(assessment);
        
        var result = await _sut.GetAssessment(assessmentId);
        
        result.ShouldNotBeNull();

        result.Id.ShouldBe(assessmentId);
        result.MaterialId.ShouldBe(materialId);
    }
    
    [Fact]
    public async Task GetAssessment_NotFound_ReturnsNull()
    {
        _repo
            .Setup(x => x.GetAssessmentByMaterialIdAsync(
                It.IsAny<Guid>(),
                _userId))
            .ReturnsAsync((Assessment?)null);
        
        var result = await _sut.GetAssessment(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAssessment_ShouldUseCurrentUser()
    {
        var assessmentId = Guid.NewGuid();
        
        _repo
            .Setup(x => x.GetAssessmentByMaterialIdAsync(
                assessmentId,
                _userId))
            .ReturnsAsync(new Assessment
            {
                Id = assessmentId,
                MaterialId = Guid.NewGuid(),
                UserId = _userId
            });

        await _sut.GetAssessment(assessmentId);
        
        _repo.Verify(
            x => x.GetAssessmentByMaterialIdAsync(
                assessmentId,
                _userId),
            Times.Once);
    }

    private Assessment SetupAssessment()
    {
        var assessment = new Assessment { Id = Guid.NewGuid(), MaterialId = Guid.NewGuid(), UserId = _userId };

        _repo
            .Setup(x => x.GetAssessmentByMaterialIdAsync(assessment.Id, _userId))
            .ReturnsAsync(assessment);

        return assessment;
    }

    [Fact]
    public async Task UpdateSettings_SavesLevelsAndCount()
    {
        var assessment = SetupAssessment();

        var result = await _sut.UpdateSettings(assessment.Id, new AssessmentSettingsDto
        {
            TimerMinutes = 30,
            Difficulties = [QuestionDifficulty.Medium, QuestionDifficulty.Easy, QuestionDifficulty.Easy],
            QuestionCount = 20,
        });

        assessment.TimerMinutes.ShouldBe(30);
        assessment.Difficulties.ShouldBe([QuestionDifficulty.Easy, QuestionDifficulty.Medium]);
        assessment.QuestionCount.ShouldBe(20);
        result.ShouldNotBeNull();
        result.Difficulties.ShouldBe([QuestionDifficulty.Easy, QuestionDifficulty.Medium]);
        result.QuestionCount.ShouldBe(20);
        _repo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateSettings_EveryLevel_IsStoredAsAll()
    {
        var assessment = SetupAssessment();
        assessment.Difficulties = [QuestionDifficulty.Hard];

        var result = await _sut.UpdateSettings(assessment.Id, new AssessmentSettingsDto
        {
            Difficulties = [QuestionDifficulty.Hard, QuestionDifficulty.Easy, QuestionDifficulty.Medium],
        });

        assessment.Difficulties.ShouldBeEmpty();
        result!.Difficulties.Count.ShouldBe(3);
    }

    [Fact]
    public async Task UpdateSettings_NoLevelsOrCount_TakesEverything()
    {
        var assessment = SetupAssessment();
        assessment.Difficulties = [QuestionDifficulty.Easy];
        assessment.QuestionCount = 10;

        await _sut.UpdateSettings(assessment.Id, new AssessmentSettingsDto());

        assessment.Difficulties.ShouldBeEmpty();
        assessment.QuestionCount.ShouldBeNull();
        assessment.TimerMinutes.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public async Task UpdateSettings_CountOutOfRange_IsRejected(int count)
    {
        var assessment = SetupAssessment();

        await Should.ThrowAsync<BadRequestException>(() =>
            _sut.UpdateSettings(assessment.Id, new AssessmentSettingsDto { QuestionCount = count }));

        _repo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateSettings_UnknownLevel_IsRejected()
    {
        var assessment = SetupAssessment();

        await Should.ThrowAsync<BadRequestException>(() =>
            _sut.UpdateSettings(assessment.Id, new AssessmentSettingsDto { Difficulties = [(QuestionDifficulty)42] }));
    }

    [Fact]
    public async Task UpdateSettings_NotFound_ReturnsNull()
    {
        var result = await _sut.UpdateSettings(Guid.NewGuid(), new AssessmentSettingsDto { QuestionCount = 5 });

        result.ShouldBeNull();
    }
}
