using Application.Constants;
using Application.DTOs.Material;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Material;
using Moq;
using Shouldly;
using Xunit;

namespace UnitTests.Services;

public class MaterialServiceTests
{
    private readonly Mock<IMaterialRepository> _materialRepo = new();
    private readonly Mock<IAssessmentRepository> _assessmentRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly MaterialService _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public MaterialServiceTests()
    {
        _userContext.Setup(x => x.UserId).Returns(_userId);

        _sut = new MaterialService(
            _materialRepo.Object,
            _assessmentRepo.Object,
            _userContext.Object);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMappedMaterials()
    {
        var materials = new List<Material>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Article",
                Type = MaterialType.article,
                Status = MaterialStatus.tolearn,
                IsActive = true,
                CreatedAt =  DateTime.UtcNow,
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Video",
                Type = MaterialType.video,
                Status = MaterialStatus.finished,
                IsActive = true,
                CreatedAt =  DateTime.UtcNow,
            }
        };

        _materialRepo
            .Setup(x => x.GetAllAsync(_userId))
            .ReturnsAsync(materials);

        var result = await _sut.GetAllAsync();

        result.Count.ShouldBe(2);
        result[0].Title.ShouldBe("Article");
        result[1].Title.ShouldBe("Video");
    }
    
    [Fact]
    public async Task GetAllAsync_NoMaterials_ReturnsEmptyList()
    {
        _materialRepo
            .Setup(x => x.GetAllAsync(_userId))
            .ReturnsAsync([]);

        var result = await _sut.GetAllAsync();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_MaterialExists_ReturnsDto()
    {
        var materialId = Guid.NewGuid();

        var material = new Material
        {
            Id = materialId,
            UserId = _userId,
            Title = "Test",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt =  DateTime.UtcNow,
        };

        _materialRepo
            .Setup(x => x.GetByIdAsync(materialId, _userId))
            .ReturnsAsync(material);

        var result = await _sut.GetByIdAsync(materialId);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(materialId);
        result.Title.ShouldBe("Test");
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        _materialRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Material?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_UnderLimit_CreatesMaterial()
    {
        _materialRepo
            .Setup(x => x.CountByUserIdAsync(_userId))
            .ReturnsAsync(5);

        var dto = new MaterialCreateDto
        {
            Title = "Material",
            Type = MaterialType.video,
            Status = MaterialStatus.tolearn
        };

        var result = await _sut.CreateAsync(dto);

        result.Title.ShouldBe("Material");

        _materialRepo.Verify(x => x.AddAsync(It.IsAny<Material>()), Times.Once);
        _assessmentRepo.Verify(x => x.AddAssessmentAsync(It.IsAny<Assessment>()), Times.Once);
        _materialRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_LimitReached_ThrowsException()
    {
        _materialRepo
            .Setup(x => x.CountByUserIdAsync(_userId))
            .ReturnsAsync(UserLimits.MaxMaterials);

        var dto = new MaterialCreateDto
        {
            Title = "Test",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn
        };

        await Should.ThrowAsync<LimitExceededException>(
            () => _sut.CreateAsync(dto));

        _materialRepo.Verify(x => x.AddAsync(It.IsAny<Material>()), Times.Never);
        _assessmentRepo.Verify(x => x.AddAssessmentAsync(It.IsAny<Assessment>()), Times.Never);
    }
    
    [Fact]
    public async Task CreateAsync_MapsDtoToMaterialCorrectly()
    {
        _materialRepo
            .Setup(x => x.CountByUserIdAsync(_userId))
            .ReturnsAsync(0);

        var dto = new MaterialCreateDto
        {
            Title = "Clean Architecture",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn
        };

        await _sut.CreateAsync(dto);

        _materialRepo.Verify(x => x.AddAsync(It.Is<Material>(m =>
            m.Title == dto.Title &&
            m.Type == dto.Type &&
            m.Status == dto.Status &&
            m.UserId == _userId &&
            m.IsActive
        )), Times.Once);
    }
    
    [Fact]
    public async Task CreateAsync_CreatesAssessmentForMaterial()
    {
        _materialRepo
            .Setup(x => x.CountByUserIdAsync(_userId))
            .ReturnsAsync(0);

        await _sut.CreateAsync(new MaterialCreateDto
        {
            Title = "Test",
            Type = MaterialType.video,
            Status = MaterialStatus.tolearn
        });

        _assessmentRepo.Verify(x =>
                x.AddAssessmentAsync(It.Is<Assessment>(a =>
                    a.UserId == _userId &&
                    a.MaterialId != Guid.Empty
                )),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_MaterialExists_UpdatesMaterial()
    {
        var materialId = Guid.NewGuid();

        var material = new Material
        {
            Id = materialId,
            UserId = _userId,
            Title = "Old",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt =  DateTime.UtcNow,
        };

        _materialRepo
            .Setup(x => x.GetByIdAsync(materialId, _userId))
            .ReturnsAsync(material);

        var dto = new MaterialUpdateDto
        {
            Title = "New title"
        };

        var result = await _sut.UpdateAsync(materialId, dto);

        result.ShouldNotBeNull();
        result.Title.ShouldBe("New title");

        _materialRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_MaterialNotFound_ReturnsNull()
    {
        _materialRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Material?)null);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), new MaterialUpdateDto());

        result.ShouldBeNull();

        _materialRepo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }
    
    [Fact]
    public async Task UpdateAsync_UpdatesStatus()
    {
        var materialId = Guid.NewGuid();

        var material = new Material
        {
            Id = materialId,
            UserId = _userId,
            Title = "Material",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _materialRepo
            .Setup(x => x.GetByIdAsync(materialId, _userId))
            .ReturnsAsync(material);

        var dto = new MaterialUpdateDto
        {
            Status = MaterialStatus.finished
        };

        var result = await _sut.UpdateAsync(materialId, dto);

        result.ShouldNotBeNull();
        result.Status.ShouldBe(MaterialStatus.finished);

        _materialRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }
    
    [Fact]
    public async Task UpdateAsync_UpdatesType()
    {
        var materialId = Guid.NewGuid();

        var material = new Material
        {
            Id = materialId,
            UserId = _userId,
            Title = "Material",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _materialRepo
            .Setup(x => x.GetByIdAsync(materialId, _userId))
            .ReturnsAsync(material);

        var dto = new MaterialUpdateDto
        {
            Type = MaterialType.video
        };

        var result = await _sut.UpdateAsync(materialId, dto);

        result.ShouldNotBeNull();
        result.Type.ShouldBe(MaterialType.video);
    }

    [Fact]
    public async Task AddTagToMaterialAsync_MaterialExists_ReturnsUpdatedMaterial()
    {
        var materialId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        var material = new Material
        {
            Id = materialId,
            UserId = _userId,
            Title = "Test",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt =  DateTime.UtcNow.AddDays(-1),
            UpdatedAt =   DateTime.UtcNow,
        };
        
        var updatedMaterial = new Material
        {
            Id = materialId,
            UserId = _userId,
            Title = "Test",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            MaterialTags =
            [
                new MaterialTag
                {
                    MaterialId = materialId,
                    TagId = tagId,
                    Tag = new Tag
                    {
                        Id = tagId,
                        Name = "Backend",
                        Color = "#ffffff"
                    }
                }
            ],
            CreatedAt =  DateTime.UtcNow.AddDays(-1),
            UpdatedAt =   DateTime.UtcNow,
        };

        _materialRepo
            .SetupSequence(x => x.GetByIdAsync(materialId, _userId))
            .ReturnsAsync(material)
            .ReturnsAsync(updatedMaterial);

        var result = await _sut.AddTagToMaterialAsync(materialId, tagId);

        result.ShouldNotBeNull();

        _materialRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddTagToMaterialAsync_MaterialNotFound_ReturnsNull()
    {
        _materialRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Material?)null);

        var result = await _sut.AddTagToMaterialAsync(Guid.NewGuid(), Guid.NewGuid());

        result.ShouldBeNull();

        _materialRepo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteTagFromMaterialAsync_MaterialExists_ReturnsUpdatedMaterial()
    {
        var materialId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        var material = new Material
        {
            Id = materialId,
            UserId = _userId,
            Title = "Test",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt =  DateTime.UtcNow,
        };

        _materialRepo
            .SetupSequence(x => x.GetByIdAsync(materialId, _userId))
            .ReturnsAsync(material)
            .ReturnsAsync(material);

        var result = await _sut.DeleteTagFromMaterialAsync(materialId, tagId);

        result.ShouldNotBeNull();

        _materialRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteTagFromMaterialAsync_MaterialNotFound_ReturnsNull()
    {
        _materialRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Material?)null);

        var result = await _sut.DeleteTagFromMaterialAsync(Guid.NewGuid(), Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsRepositoryResult()
    {
        var materialId = Guid.NewGuid();

        _materialRepo
            .Setup(x => x.DeleteAsync(materialId, _userId))
            .ReturnsAsync(true);

        var result = await _sut.DeleteAsync(materialId);

        result.ShouldBeTrue();

        _materialRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }
    
    [Fact]
    public async Task DeleteAsync_RepositoryReturnsFalse_ReturnsFalse()
    {
        var materialId = Guid.NewGuid();

        _materialRepo
            .Setup(x => x.DeleteAsync(materialId, _userId))
            .ReturnsAsync(false);

        var result = await _sut.DeleteAsync(materialId);

        result.ShouldBeFalse();

        _materialRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectStatistics()
    {
        var materials = new List<Material>
        {
            new()
            {
                UserId = _userId,
                Title = "A",
                Type = MaterialType.article,
                Status = MaterialStatus.tolearn,
                IsActive = true,
                CreatedAt =  DateTime.UtcNow,
            },
            new()
            {
                UserId = _userId,
                Title = "B",
                Type = MaterialType.article,
                Status = MaterialStatus.finished,
                IsActive = true,
                CreatedAt =  DateTime.UtcNow,
            },
            new()
            {
                UserId = _userId,
                Title = "C",
                Type = MaterialType.video,
                Status = MaterialStatus.finished,
                IsActive = true,
                CreatedAt =  DateTime.UtcNow,
            }
        };

        _materialRepo
            .Setup(x => x.GetAllAsync(_userId))
            .ReturnsAsync(materials);

        var result = await _sut.GetStatisticsAsync();

        result.Count.ShouldBe(3);

        result.Statuses[MaterialStatus.finished].ShouldBe(2);
        result.Statuses[MaterialStatus.tolearn].ShouldBe(1);

        result.Types[MaterialType.article].ShouldBe(2);
        result.Types[MaterialType.video].ShouldBe(1);
    }
    
    [Fact]
    public async Task GetStatisticsAsync_NoMaterials_ReturnsEmptyStatistics()
    {
        _materialRepo
            .Setup(x => x.GetAllAsync(_userId))
            .ReturnsAsync([]);

        var result = await _sut.GetStatisticsAsync();

        result.Count.ShouldBe(0);
        result.Statuses.ShouldBeEmpty();
        result.Types.ShouldBeEmpty();
    }
}