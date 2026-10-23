using Application.Constants;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Material;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class MaterialShareServiceTests
{
    private readonly Mock<IMaterialShareRepository> _shareRepo = new();
    private readonly Mock<IMaterialRepository> _materialRepo = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<ILimitLock> _limitLock = new();
    private readonly Mock<ILimitLockScope> _limitLockScope = new();
    private readonly MaterialShareService _sut;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _recipientId = Guid.NewGuid();

    public MaterialShareServiceTests()
    {
        _limitLock
            .Setup(x => x.AcquireAsync(It.IsAny<LimitScope>(), It.IsAny<Guid>()))
            .ReturnsAsync(_limitLockScope.Object);

        _sut = new MaterialShareService(_shareRepo.Object, _materialRepo.Object, _user.Object, _limitLock.Object);
    }

    private void SignIn(Guid? userId)
    {
        _user.Setup(x => x.CurrentUserId).Returns(userId);
        _user.Setup(x => x.UserId).Returns(userId ?? Guid.Empty);
    }

    private Material CreateMaterial() => new()
    {
        Id = Guid.NewGuid(),
        UserId = _ownerId,
        Title = "Shared",
        Type = MaterialType.summary,
        Status = MaterialStatus.inprocess,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private MaterialShare CreateShare(Material material) => new()
    {
        Id = Guid.NewGuid(),
        MaterialId = material.Id,
        Material = material,
        UserId = _ownerId,
        Token = "token-123",
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task CreateShareAsync_NotOwnMaterial_ReturnsNull()
    {
        SignIn(_ownerId);

        var result = await _sut.CreateShareAsync(Guid.NewGuid());

        result.ShouldBeNull();
        _shareRepo.Verify(x => x.AddAsync(It.IsAny<MaterialShare>()), Times.Never);
    }

    [Fact]
    public async Task CreateShareAsync_NewLink_HasUrlSafeToken()
    {
        SignIn(_ownerId);
        var material = CreateMaterial();
        MaterialShare? saved = null;

        _materialRepo.Setup(x => x.GetByIdAsync(material.Id, _ownerId)).ReturnsAsync(material);
        _shareRepo.Setup(x => x.AddAsync(It.IsAny<MaterialShare>())).Callback<MaterialShare>(s => saved = s);

        var result = await _sut.CreateShareAsync(material.Id);

        result.ShouldNotBeNull();
        saved.ShouldNotBeNull();
        result.Token.ShouldBe(saved.Token);
        result.Token.Length.ShouldBe(24);
        result.Token.ShouldAllBe(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        _shareRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateShareAsync_AlreadyShared_ReturnsSameLink()
    {
        SignIn(_ownerId);
        var material = CreateMaterial();
        var share = CreateShare(material);

        _materialRepo.Setup(x => x.GetByIdAsync(material.Id, _ownerId)).ReturnsAsync(material);
        _shareRepo.Setup(x => x.GetActiveByMaterialIdAsync(material.Id, _ownerId)).ReturnsAsync(share);

        var result = await _sut.CreateShareAsync(material.Id);

        result!.Token.ShouldBe(share.Token);
        _shareRepo.Verify(x => x.AddAsync(It.IsAny<MaterialShare>()), Times.Never);
    }

    [Fact]
    public async Task RevokeShareAsync_ActiveLink_SetsRevokedAt()
    {
        SignIn(_ownerId);
        var share = CreateShare(CreateMaterial());

        _shareRepo.Setup(x => x.GetActiveByMaterialIdAsync(share.MaterialId, _ownerId)).ReturnsAsync(share);

        var result = await _sut.RevokeShareAsync(share.MaterialId);

        result.ShouldBeTrue();
        share.RevokedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetPreviewAsync_Anonymous_ShowsContentWithoutUserFlags()
    {
        SignIn(null);
        var share = CreateShare(CreateMaterial());

        _shareRepo.Setup(x => x.GetActiveByTokenAsync(share.Token)).ReturnsAsync(share);

        var result = await _sut.GetPreviewAsync(share.Token);

        result.ShouldNotBeNull();
        result.Title.ShouldBe("Shared");
        result.IsOwner.ShouldBeFalse();
        result.IsAlreadyImported.ShouldBeFalse();
        _shareRepo.Verify(x => x.HasImportedAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetPreviewAsync_RecipientWhoImported_IsMarked()
    {
        SignIn(_recipientId);
        var share = CreateShare(CreateMaterial());

        _shareRepo.Setup(x => x.GetActiveByTokenAsync(share.Token)).ReturnsAsync(share);
        _shareRepo.Setup(x => x.HasImportedAsync(share.MaterialId, _recipientId)).ReturnsAsync(true);

        var result = await _sut.GetPreviewAsync(share.Token);

        result!.IsAlreadyImported.ShouldBeTrue();
        result.IsOwner.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPreviewAsync_UnknownOrTooLongToken_ReturnsNull()
    {
        SignIn(null);

        (await _sut.GetPreviewAsync("missing")).ShouldBeNull();
        (await _sut.GetPreviewAsync(new string('a', 65))).ShouldBeNull();
    }

    [Fact]
    public async Task ImportAsync_CopiesMaterialForRecipient()
    {
        SignIn(_recipientId);
        var share = CreateShare(CreateMaterial());
        Material? added = null;

        _shareRepo.Setup(x => x.GetActiveByTokenAsync(share.Token)).ReturnsAsync(share);
        _materialRepo.Setup(x => x.AddAsync(It.IsAny<Material>())).Callback<Material>(m => added = m);

        var result = await _sut.ImportAsync(share.Token);

        result.ShouldNotBeNull();
        result.IsImported.ShouldBeTrue();
        added.ShouldNotBeNull();
        added.UserId.ShouldBe(_recipientId);
        added.ImportedFromMaterialId.ShouldBe(share.MaterialId);
        share.ImportsCount.ShouldBe(1);
        _materialRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
        // Import takes a material place, so it shares the lock with creating a material
        _limitLock.Verify(x => x.AcquireAsync(LimitScope.Materials, _recipientId), Times.Once);
        _limitLockScope.Verify(x => x.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_MaterialLimitReached_Throws()
    {
        SignIn(_recipientId);
        var share = CreateShare(CreateMaterial());

        _shareRepo.Setup(x => x.GetActiveByTokenAsync(share.Token)).ReturnsAsync(share);
        _materialRepo.Setup(x => x.CountByUserIdAsync(_recipientId)).ReturnsAsync(UserLimits.MaxMaterials);

        await Should.ThrowAsync<LimitExceededException>(() => _sut.ImportAsync(share.Token));

        _materialRepo.Verify(x => x.AddAsync(It.IsAny<Material>()), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_RevokedOrUnknownLink_ReturnsNull()
    {
        SignIn(_recipientId);

        var result = await _sut.ImportAsync("revoked");

        result.ShouldBeNull();
    }
}
