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
    Task SaveChangesAsync();
}