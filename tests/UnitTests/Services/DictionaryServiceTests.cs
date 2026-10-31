using Application.Constants;
using Application.Limits;
using Application.DTOs.Dictionary;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Material;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class DictionaryServiceTests
{
    private readonly Mock<IDictionaryRepository> _dictionaryRepo = new();
    private readonly Mock<IMaterialRepository> _materialRepo = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<ILimitLock> _limitLock = new();
    private readonly Mock<ILimitLockScope> _limitLockScope = new();
    private readonly DictionaryService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Material _material;
    private readonly Dictionary _dictionary;

    public DictionaryServiceTests()
    {
        _user.Setup(x => x.UserId).Returns(_userId);

        _limitLock
            .Setup(x => x.AcquireAsync(It.IsAny<LimitScope>(), It.IsAny<Guid>()))
            .ReturnsAsync(_limitLockScope.Object);

        _material = new Material
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = "Material",
            Type = MaterialType.article,
            Status = MaterialStatus.tolearn,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        _dictionary = new Dictionary
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MaterialId = _material.Id,
            Title = "Words",
            CreatedAt = DateTime.UtcNow,
        };

        _materialRepo.Setup(x => x.GetByIdAsync(_material.Id, _userId)).ReturnsAsync(_material);
        _dictionaryRepo.Setup(x => x.GetByIdAsync(_dictionary.Id, _userId)).ReturnsAsync(_dictionary);

        _sut = new DictionaryService(_dictionaryRepo.Object, _materialRepo.Object, _user.Object, _limitLock.Object);
    }

    [Fact]
    public async Task CreateAsync_UnderLimit_CountsUnderMaterialLockAndCommits()
    {
        _dictionaryRepo.Setup(x => x.CountByMaterialIdAsync(_material.Id, _userId))
            .ReturnsAsync(ContentLimitDefaults.Free.DictionariesPerMaterial!.Value - 1);

        var result = await _sut.CreateAsync(_material.Id, new DictionaryCreateDto { Title = "Second" });

        result.ShouldNotBeNull();
        _limitLock.Verify(x => x.AcquireAsync(LimitScope.Dictionaries, _material.Id), Times.Once);
        _limitLockScope.Verify(x => x.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_LimitReached_Throws()
    {
        _dictionaryRepo.Setup(x => x.CountByMaterialIdAsync(_material.Id, _userId))
            .ReturnsAsync(ContentLimitDefaults.Free.DictionariesPerMaterial!.Value);

        await Should.ThrowAsync<LimitExceededException>(() =>
            _sut.CreateAsync(_material.Id, new DictionaryCreateDto { Title = "Third" }));

        _dictionaryRepo.Verify(x => x.AddAsync(It.IsAny<Dictionary>()), Times.Never);
        _limitLockScope.Verify(x => x.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateEntryAsync_UnderLimit_TakesNextPositionAndCommits()
    {
        _dictionaryRepo.Setup(x => x.CountEntriesAsync(_dictionary.Id))
            .ReturnsAsync(ContentLimitDefaults.Free.EntriesPerDictionary!.Value - 1);
        _dictionaryRepo.Setup(x => x.GetMaxEntryPositionAsync(_dictionary.Id)).ReturnsAsync(98);

        var result = await _sut.CreateEntryAsync(_dictionary.Id,
            new DictionaryEntryCreateDto { Term = "Baum", Translation = "tree" });

        result.ShouldNotBeNull();
        result.Position.ShouldBe(99);
        _limitLock.Verify(x => x.AcquireAsync(LimitScope.DictionaryEntries, _dictionary.Id), Times.Once);
        _limitLockScope.Verify(x => x.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateEntryAsync_LimitReached_Throws()
    {
        _dictionaryRepo.Setup(x => x.CountEntriesAsync(_dictionary.Id))
            .ReturnsAsync(ContentLimitDefaults.Free.EntriesPerDictionary!.Value);

        await Should.ThrowAsync<LimitExceededException>(() =>
            _sut.CreateEntryAsync(_dictionary.Id,
                new DictionaryEntryCreateDto { Term = "Baum", Translation = "tree" }));

        _dictionaryRepo.Verify(x => x.AddEntryAsync(It.IsAny<DictionaryEntry>()), Times.Never);
        _limitLockScope.Verify(x => x.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateEntryAsync_ForeignDictionary_ReturnsNullWithoutLocking()
    {
        var result = await _sut.CreateEntryAsync(Guid.NewGuid(),
            new DictionaryEntryCreateDto { Term = "Baum", Translation = "tree" });

        result.ShouldBeNull();
        _limitLock.Verify(x => x.AcquireAsync(It.IsAny<LimitScope>(), It.IsAny<Guid>()), Times.Never);
    }
}
