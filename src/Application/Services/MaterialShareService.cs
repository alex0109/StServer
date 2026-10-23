using System.Security.Cryptography;
using Application.Constants;
using Application.DTOs.Material;
using Application.DTOs.Share;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;

namespace Application.Services;

public class MaterialShareService : IMaterialShareService
{
    private const int MaxTokenLength = 64;

    private readonly IMaterialShareRepository _shareRepository;
    private readonly IMaterialRepository _materialRepository;
    private readonly IUserContext _user;
    private readonly ILimitLock _limitLock;

    public MaterialShareService(
        IMaterialShareRepository shareRepository,
        IMaterialRepository materialRepository,
        IUserContext user,
        ILimitLock limitLock)
    {
        _shareRepository = shareRepository;
        _materialRepository = materialRepository;
        _user = user;
        _limitLock = limitLock;
    }

    public async Task<MaterialShareResponseDto?> GetShareAsync(Guid materialId)
    {
        var share = await _shareRepository.GetActiveByMaterialIdAsync(materialId, _user.UserId);

        return share is null ? null : MaterialShareMapper.ToDto(share);
    }

    public async Task<MaterialShareResponseDto?> CreateShareAsync(Guid materialId)
    {
        var userId = _user.UserId;

        var material = await _materialRepository.GetByIdAsync(materialId, userId);
        if (material is null) return null;

        // One active link per material, sharing twice returns the same link
        var existing = await _shareRepository.GetActiveByMaterialIdAsync(materialId, userId);
        if (existing is not null) return MaterialShareMapper.ToDto(existing);

        var share = MaterialShareMapper.ToEntity(materialId, userId, GenerateToken(), DateTime.UtcNow);

        await _shareRepository.AddAsync(share);
        await _shareRepository.SaveChangesAsync();

        return MaterialShareMapper.ToDto(share);
    }

    public async Task<bool> RevokeShareAsync(Guid materialId)
    {
        var share = await _shareRepository.GetActiveByMaterialIdAsync(materialId, _user.UserId);
        if (share is null) return false;

        share.RevokedAt = DateTime.UtcNow;
        await _shareRepository.SaveChangesAsync();

        return true;
    }

    public async Task<SharedMaterialPreviewDto?> GetPreviewAsync(string token)
    {
        if (!IsValidToken(token)) return null;

        var share = await _shareRepository.GetActiveByTokenAsync(token);
        if (share is null) return null;

        var userId = _user.CurrentUserId;
        var isOwner = userId == share.UserId;
        var isAlreadyImported = userId is Guid id && await _shareRepository.HasImportedAsync(share.MaterialId, id);

        return MaterialShareMapper.ToPreviewDto(share, isOwner, isAlreadyImported);
    }

    public async Task<MaterialResponseDto?> ImportAsync(string token)
    {
        var userId = _user.UserId;

        if (!IsValidToken(token)) return null;

        var share = await _shareRepository.GetActiveByTokenAsync(token);
        if (share is null) return null;

        // Same lock as creating a material, an import also takes a material place
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.Materials, userId);

        var count = await _materialRepository.CountByUserIdAsync(userId);
        if (count >= UserLimits.MaxMaterials)
        {
            throw new LimitExceededException($"Material limit reached: {UserLimits.MaxMaterials}");
        }

        var copy = MaterialCopyMapper.Copy(share.Material, userId, DateTime.UtcNow);
        share.ImportsCount++;

        await _materialRepository.AddAsync(copy);
        await _materialRepository.SaveChangesAsync();
        await limitLock.CommitAsync();

        return MaterialMapper.ToDto(copy);
    }

    // 18 random bytes, url safe base64 without padding: 24 characters
    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool IsValidToken(string token) =>
        !string.IsNullOrWhiteSpace(token) && token.Length <= MaxTokenLength;
}
