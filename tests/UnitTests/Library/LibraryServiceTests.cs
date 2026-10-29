using Application.Constants;
using Application.DTOs.Library;
using Application.Exceptions;
using Application.Interfaces;
using Application.Library;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Library;
using Domain.Utility.Material;
using Moq;
using Shouldly;

namespace UnitTests.Library;

public class LibraryServiceTests
{
    private readonly Guid _author = Guid.NewGuid();
    private readonly Guid _reader = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly FakeLibraryRepository _library = new();
    private readonly Mock<IMaterialRepository> _materials = new();
    private readonly Mock<ILimitLock> _lock = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly List<Material> _added = [];

    public LibraryServiceTests()
    {
        _lock.Setup(x => x.AcquireAsync(It.IsAny<LimitScope>(), It.IsAny<Guid>()))
            .ReturnsAsync(new Mock<ILimitLockScope>().Object);
        _materials.Setup(x => x.AddAsync(It.IsAny<Material>()))
            .Callback<Material>(m => _added.Add(m))
            .Returns(Task.CompletedTask);
    }

    private LibraryService AsUser(Guid userId)
    {
        _user.Setup(x => x.UserId).Returns(userId);
        return new LibraryService(_library, _materials.Object, _user.Object, _lock.Object, TimeProvider.System);
    }

    private LibraryAdminService AsAdmin()
    {
        var admin = new Mock<IUserContext>();
        admin.Setup(x => x.UserId).Returns(_admin);
        return new LibraryAdminService(_library, admin.Object, TimeProvider.System);
    }

    private Material AuthorMaterial()
    {
        var material = LibraryTestData.Material(_author);
        _library.Materials.Add(material);
        return material;
    }

    private async Task<LibraryItem> PublishedItemAsync()
    {
        var material = AuthorMaterial();
        var request = await AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = "Olena",
        });
        await AsAdmin().ApproveAsync(request.Id);

        return _library.Items.Single();
    }

    [Fact]
    public async Task PublishRequest_WaitsForReview_AndIsNotInTheLibraryYet()
    {
        var material = AuthorMaterial();

        var request = await AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = "  Olena   K.  ", Comment = "For biology class",
        });

        request.Status.ShouldBe(LibraryRequestStatus.Pending);
        request.AuthorNickname.ShouldBe("Olena K.");
        _library.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a")]
    [InlineData("https://spam.example")]
    [InlineData("<script>")]
    public async Task PublishRequest_NeedsAProperNickname(string? nickname)
    {
        var material = AuthorMaterial();

        await Should.ThrowAsync<BadRequestException>(() => AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = nickname,
        }));
    }

    [Fact]
    public async Task PublishRequest_ForSomeoneElsesMaterial_IsNotFound()
    {
        var material = AuthorMaterial();

        await Should.ThrowAsync<KeyNotFoundException>(() => AsUser(_reader).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = "Thief",
        }));
    }

    [Fact]
    public async Task SameMaterialTwiceWhilePending_IsRejected()
    {
        var material = AuthorMaterial();
        var dto = new LibraryRequestCreateDto { Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = "Olena" };
        await AsUser(_author).CreateRequestAsync(dto);

        await Should.ThrowAsync<ConflictException>(() => AsUser(_author).CreateRequestAsync(dto));
    }

    [Fact]
    public async Task PendingRequestsPerAuthor_AreLimited()
    {
        for (var i = 0; i < LibraryRules.MaxPendingRequests; i++)
        {
            await AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
            {
                Type = LibraryRequestType.Publish, MaterialId = AuthorMaterial().Id, AuthorNickname = "Olena",
            });
        }

        await Should.ThrowAsync<LimitExceededException>(() => AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = AuthorMaterial().Id, AuthorNickname = "Olena",
        }));
    }

    [Fact]
    public async Task MaterialImportedFromTheLibrary_CantBeSentBack()
    {
        var material = AuthorMaterial();
        material.ImportedFromLibraryItemId = Guid.NewGuid();

        await Should.ThrowAsync<BadRequestException>(() => AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = "Olena",
        }));
    }

    [Fact]
    public async Task Approval_PublishesTheSnapshotTakenAtSending()
    {
        var material = AuthorMaterial();
        var request = await AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = "Olena",
        });

        // Changes after sending don't reach the library
        material.Title = "Changed later";
        await AsAdmin().ApproveAsync(request.Id);

        var item = _library.Items.Single();
        item.Title.ShouldBe("Photosynthesis");
        item.AuthorNickname.ShouldBe("Olena");
        item.Status.ShouldBe(LibraryItemStatus.Published);
        (item.NotesCount, item.QuestionsCount, item.DictionariesCount, item.WordsCount).ShouldBe((2, 2, 1, 2));
        _library.Requests.Single().Status.ShouldBe(LibraryRequestStatus.Approved);

        var view = await AsUser(_reader).GetAsync(item.Id);
        view.ShouldNotBeNull();
        view.IsMine.ShouldBeFalse();
        view.Content.Notes.Count.ShouldBe(2);
    }

    [Fact]
    public async Task HandledRequest_CantBeApprovedAgain()
    {
        var material = AuthorMaterial();
        var request = await AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = "Olena",
        });
        await AsAdmin().RejectAsync(request.Id, LibraryRejectionReason.Title, "Not about studying");

        await Should.ThrowAsync<ConflictException>(() => AsAdmin().ApproveAsync(request.Id));
        _library.Items.ShouldBeEmpty();
        var mine = (await AsUser(_author).GetMyRequestsAsync()).Single();
        mine.RejectionReason.ShouldBe(LibraryRejectionReason.Title);
        mine.ReviewComment.ShouldBe("Not about studying");
    }

    [Fact]
    public async Task UnknownRejectionReason_IsRefused()
    {
        var material = AuthorMaterial();
        var request = await AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = "Olena",
        });

        await Should.ThrowAsync<BadRequestException>(() => AsAdmin().RejectAsync(request.Id, (LibraryRejectionReason)42, null));
    }

    [Fact]
    public async Task UpdateRequest_ReplacesContentOnlyAfterApproval()
    {
        var item = await PublishedItemAsync();
        var newVersion = AuthorMaterial();
        newVersion.Title = "Photosynthesis, part 2";

        var request = await AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Update, LibraryItemId = item.Id, MaterialId = newVersion.Id, AuthorNickname = "Olena",
        });

        (await AsUser(_author).GetAsync(item.Id))!.PendingRequest.ShouldBe(LibraryRequestType.Update);
        item.Title.ShouldBe("Photosynthesis");

        await AsAdmin().ApproveAsync(request.Id);

        item.Title.ShouldBe("Photosynthesis, part 2");
        item.Version.ShouldBe(2);
    }

    [Fact]
    public async Task OnlyTheAuthor_CanAskForChanges()
    {
        var item = await PublishedItemAsync();

        await Should.ThrowAsync<KeyNotFoundException>(() => AsUser(_reader).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Remove, LibraryItemId = item.Id,
        }));
    }

    [Fact]
    public async Task RemoveRequest_HidesTheMaterialAfterApproval()
    {
        var item = await PublishedItemAsync();
        var request = await AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Remove, LibraryItemId = item.Id,
        });

        await AsAdmin().ApproveAsync(request.Id);

        item.Status.ShouldBe(LibraryItemStatus.Removed);
        (await AsUser(_reader).GetAsync(item.Id)).ShouldBeNull();
        (await AsUser(_reader).ImportAsync(item.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task CancelledRequest_FreesThePlace()
    {
        var material = AuthorMaterial();
        var request = await AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = "Olena",
        });

        (await AsUser(_reader).CancelRequestAsync(request.Id)).ShouldBeFalse();
        (await AsUser(_author).CancelRequestAsync(request.Id)).ShouldBeTrue();

        await Should.NotThrowAsync(() => AsUser(_author).CreateRequestAsync(new LibraryRequestCreateDto
        {
            Type = LibraryRequestType.Publish, MaterialId = material.Id, AuthorNickname = "Olena",
        }));
    }

    [Fact]
    public async Task Import_GivesTheReaderAnImportedCopy()
    {
        var item = await PublishedItemAsync();
        _materials.Setup(x => x.CountByUserIdAsync(_reader)).ReturnsAsync(0);

        var material = await AsUser(_reader).ImportAsync(item.Id);

        material.ShouldNotBeNull();
        material.IsImported.ShouldBeTrue();
        material.IsFromLibrary.ShouldBeTrue();
        _added.Single().UserId.ShouldBe(_reader);
        item.ImportsCount.ShouldBe(1);
    }

    [Fact]
    public async Task Import_RespectsTheMaterialsLimit()
    {
        var item = await PublishedItemAsync();
        _materials.Setup(x => x.CountByUserIdAsync(_reader)).ReturnsAsync(UserLimits.MaxMaterials);

        await Should.ThrowAsync<LimitExceededException>(() => AsUser(_reader).ImportAsync(item.Id));
        item.ImportsCount.ShouldBe(0);
    }

    [Fact]
    public async Task OwnerPublishing_GoesStraightToTheLibrary()
    {
        var material = LibraryTestData.Material(_admin);
        _library.Materials.Add(material);

        var item = await AsAdmin().PublishAsync(new LibraryAdminPublishDto { MaterialId = material.Id, AuthorNickname = "Studytracker" });

        item.ShouldNotBeNull();
        _library.Items.Single().Status.ShouldBe(LibraryItemStatus.Published);
    }

    private sealed class FakeLibraryRepository : ILibraryRepository
    {
        public List<Material> Materials { get; } = [];
        public List<LibraryItem> Items { get; } = [];
        public List<LibraryRequest> Requests { get; } = [];

        public Task<Material?> GetMaterialForSnapshotAsync(Guid materialId, Guid userId) =>
            Task.FromResult(Materials.FirstOrDefault(x => x.Id == materialId && x.UserId == userId));

        public Task<(List<LibraryItemSummaryDto> Items, int Total)> ListPublishedAsync(
            Guid userId, string? search, MaterialType? type, int skip, int take)
        {
            var items = Items.Where(x => x.Status == LibraryItemStatus.Published)
                .Select(x => LibraryRules.ToSummaryDto(x, userId, false)).ToList();
            return Task.FromResult((items.Skip(skip).Take(take).ToList(), items.Count));
        }

        public Task<LibraryItem?> GetItemAsync(Guid itemId) => Task.FromResult(Items.FirstOrDefault(x => x.Id == itemId));

        public Task<bool> HasImportedAsync(Guid itemId, Guid userId) => Task.FromResult(false);

        public Task AddItemAsync(LibraryItem item)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task AddRequestAsync(LibraryRequest request)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<LibraryRequest?> GetRequestAsync(Guid requestId) =>
            Task.FromResult(Requests.FirstOrDefault(x => x.Id == requestId));

        public Task<List<LibraryRequestDto>> GetUserRequestsAsync(Guid userId, int take) =>
            Task.FromResult(Requests.Where(x => x.UserId == userId).Select(LibraryRules.ToDto).ToList());

        public Task<List<LibraryRequestDetailsDto>> GetRequestsAsync(LibraryRequestStatus? status, int take) =>
            Task.FromResult(new List<LibraryRequestDetailsDto>());

        public Task<int> CountPendingAsync(Guid userId) =>
            Task.FromResult(Requests.Count(x => x.UserId == userId && x.Status == LibraryRequestStatus.Pending));

        public Task<bool> HasPendingForMaterialAsync(Guid materialId) =>
            Task.FromResult(Requests.Any(x => x.MaterialId == materialId && x.Status == LibraryRequestStatus.Pending && x.Type != LibraryRequestType.Remove));

        public Task<LibraryRequestType?> GetPendingTypeForItemAsync(Guid itemId) =>
            Task.FromResult(Requests.Where(x => x.LibraryItemId == itemId && x.Status == LibraryRequestStatus.Pending)
                .Select(x => (LibraryRequestType?)x.Type).FirstOrDefault());

        public Task SaveChangesAsync() => Task.CompletedTask;
    }
}
