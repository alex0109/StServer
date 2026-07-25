using Application.Interfaces;
using Application.Services;
using Domain.Entities;
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
}