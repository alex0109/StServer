using Application.DTOs.Material;
using Domain.Entities;

namespace Application.Interfaces;

public interface IMaterialRepository
{
    Task<List<Material>> GetAllAsync(Guid userId);
    // Without material and note content, for lists
    Task<List<MaterialResponseDto>> GetListAsync(Guid userId);
    Task<Material?> GetByIdAsync(Guid materialId, Guid userId);
    Task AddAsync(Material material);
    Task<bool> DeleteAsync(Guid materialId, Guid userId);
    Task<int> CountByUserIdAsync(Guid userId);
    // Materials the user made or got by a share link, without library copies
    Task<int> CountOwnByUserIdAsync(Guid userId);
    Task<int> CountLibraryCopiesAsync(Guid userId);
    // A tag of this user; one of another user must never be attached to a material
    Task<bool> TagExistsAsync(Guid tagId, Guid userId);
    // Bytes the user's material texts, note texts, drawings and essay reviews take in the database, as stored
    Task<long> GetStoredContentBytesAsync(Guid userId);
    Task SaveChangesAsync();
}