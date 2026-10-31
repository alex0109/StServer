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
    Task SaveChangesAsync();
}