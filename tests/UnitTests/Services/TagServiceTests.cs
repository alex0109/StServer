using Application.Constants;
using Application.DTOs.Tag;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class TagServiceTests
{
    private readonly Mock<ITagRepository> _repo = new();
    private readonly Mock<IUserContext> _user = new();

    private readonly TagService _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public TagServiceTests()
    {
        _user.Setup(x => x.UserId)
            .Returns(_userId);

        _sut = new TagService(
            _repo.Object,
            _user.Object);
    }
    
    [Fact]
    public async Task GetAllTagsAsync_ReturnsMappedTags()
    {
        var tags = new List<Tag>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Name = "Backend",
                Color = "#fff"
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Name = "Frontend",
                Color = "#000"
            }
        };

        _repo
            .Setup(x => x.GetAllTags(_userId))
            .ReturnsAsync(tags);
        
        var result = await _sut.GetAllTagsAsync();
        
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        result[0].Name.ShouldBe("Backend");
        result[1].Name.ShouldBe("Frontend");
    }
    
    [Fact]
    public async Task GetAllTagsAsync_NoTags_ReturnsNull()
    {
        _repo
            .Setup(x => x.GetAllTags(_userId))
            .ReturnsAsync((List<Tag>?)null);
        
        var result = await _sut.GetAllTagsAsync();
        
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetMaterialsByTagAsync_ReturnsMaterials()
    {
        var tagId = Guid.NewGuid();

        var materials = new List<Material>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "C#",
                Type = Domain.Utility.Material.MaterialType.article,
                Status = Domain.Utility.Material.MaterialStatus.tolearn,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        _repo
            .Setup(x => x.GetMaterialsByTagAsync(tagId, _userId))
            .ReturnsAsync(materials);
        
        var result = await _sut.GetMaterialsByTagAsync(tagId);
        
        result.ShouldNotBeNull();

        result.Count.ShouldBe(1);
        result[0].Title.ShouldBe("C#");
    }
    
    [Fact]
    public async Task GetMaterialsByTagAsync_NoMaterials_ReturnsNull()
    {
        _repo
            .Setup(x => x.GetMaterialsByTagAsync(
                It.IsAny<Guid>(),
                _userId))
            .ReturnsAsync((List<Material>?)null);
        
        var result = await _sut.GetMaterialsByTagAsync(Guid.NewGuid());
        
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_TagExists_ReturnsDto()
    {
        var tagId = Guid.NewGuid();

        var tag = new Tag
        {
            Id = tagId,
            UserId = _userId,
            Name = "Programming",
            Color = "#123"
        };
        
        _repo
            .Setup(x => x.GetTagByIdAsync(tagId, _userId))
            .ReturnsAsync(tag);
        
        var result = await _sut.GetByIdAsync(tagId);
        
        result.ShouldNotBeNull();

        result.Id.ShouldBe(tagId);
        result.Name.ShouldBe("Programming");
        result.Color.ShouldBe("#123");
    }

    [Fact]
    public async Task GetByIdAsync_TagNotFound_ReturnsNull()
    {
        _repo
            .Setup(x => x.GetTagByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>()))
            .ReturnsAsync((Tag?)null);
        
        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        
        result.ShouldBeNull();
    }
    
    [Fact]
    public async Task CreateAsync_UnderLimit_CreatesTag()
    {
        _repo
            .Setup(x => x.CountByUserIdAsync(_userId))
            .ReturnsAsync(5);

        var dto = new TagCreateDto
        {
            Name = "Backend",
            Color = "#fff"
        };
        
        _repo
            .Setup(x => x.AddTagAsync(It.IsAny<Tag>()))
            .ReturnsAsync((Tag x) => x);

        var result = await _sut.CreateAsync(dto);

        result.ShouldNotBeNull();

        result.Name.ShouldBe("Backend");
        
        _repo.Verify(
            x => x.AddTagAsync(It.IsAny<Tag>()),
            Times.Once);

        _repo.Verify(
            x => x.SaveChangesAsync(),
            Times.Once);
    }
    
    [Fact]
    public async Task CreateAsync_LimitReached_ThrowsException()
    {
        _repo
            .Setup(x => x.CountByUserIdAsync(_userId))
            .ReturnsAsync(UserLimits.MaxTags);
        
        var dto = new TagCreateDto
        {
            Name = "Test",
            Color = "#000"
        };
        
        await Should.ThrowAsync<LimitExceededException>(
            () => _sut.CreateAsync(dto));
        
        _repo.Verify(
            x => x.AddTagAsync(It.IsAny<Tag>()),
            Times.Never);
    }
    
    [Fact]
    public async Task CreateAsync_ShouldMapUserId()
    {
        _repo
            .Setup(x => x.CountByUserIdAsync(_userId))
            .ReturnsAsync(0);
        
        _repo
            .Setup(x => x.AddTagAsync(It.IsAny<Tag>()))
            .ReturnsAsync((Tag t) => t);

        await _sut.CreateAsync(new TagCreateDto
        {
            Name = "CSharp",
            Color = "red"
        });
        
        _repo.Verify(
            x => x.AddTagAsync(It.Is<Tag>(t =>
                t.UserId == _userId &&
                t.Name == "CSharp" &&
                t.Color == "red")),
            Times.Once);
    }

    [Fact]
    public async Task UpdateTagAsync_TagExists_UpdatesTag()
    {
        var tagId = Guid.NewGuid();

        var tag = new Tag
        {
            Id = tagId,
            UserId = _userId,
            Name = "Old",
            Color = "black"
        };
        
        _repo
            .Setup(x => x.GetTagByIdAsync(tagId, _userId))
            .ReturnsAsync(tag);

        var result = await _sut.UpdateTagAsync(
            tagId,
            new TagUpdateDto
            {
                Name = "New"
            });
        
        result.ShouldNotBeNull();
        
        result.Name.ShouldBe("New");
        
        _repo.Verify(
            x => x.SaveChangesAsync(),
            Times.Once);
    }
    
    [Fact]
    public async Task UpdateTagAsync_TagNotFound_ReturnsNull()
    {
        _repo
            .Setup(x => x.GetTagByIdAsync(
                It.IsAny<Guid>(),
                _userId))
            .ReturnsAsync((Tag?)null);
        
        var result = await _sut.UpdateTagAsync(
            Guid.NewGuid(),
            new TagUpdateDto());
        
        result.ShouldBeNull();
        
        _repo.Verify(
            x => x.SaveChangesAsync(),
            Times.Never);
    }
    
    [Fact]
    public async Task UpdateTagAsync_UpdatesColor()
    {
        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Name = "Tag",
            Color = "black"
        };
        
        _repo
            .Setup(x => x.GetTagByIdAsync(
                tag.Id,
                _userId))
            .ReturnsAsync(tag);
        
        var result = await _sut.UpdateTagAsync(
            tag.Id,
            new TagUpdateDto
            {
                Color = "white"
            });
        
        result!.Color.ShouldBe("white");
    }
    
    [Fact]
    public async Task DeleteTagAsync_ReturnsTrue()
    {
        var tagId = Guid.NewGuid();
        
        _repo
            .Setup(x => x.DeleteTagAsync(tagId, _userId))
            .ReturnsAsync(true);
        
        var result = await _sut.DeleteTagAsync(tagId);
        
        result.ShouldBeTrue();
        
        _repo.Verify(
            x => x.DeleteTagAsync(tagId, _userId),
            Times.Once);
        
        _repo.Verify(
            x => x.SaveChangesAsync(),
            Times.Once);
    }
    
    [Fact]
    public async Task DeleteTagAsync_ReturnsFalse()
    {
        _repo
            .Setup(x => x.DeleteTagAsync(
                It.IsAny<Guid>(),
                _userId))
            .ReturnsAsync(false);

        var result = await _sut.DeleteTagAsync(Guid.NewGuid());
        
        result.ShouldBeFalse();
        
        _repo.Verify(
            x => x.SaveChangesAsync(),
            Times.Once);
    }
}