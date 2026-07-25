using Domain.Entities;

namespace Application.Interfaces;

public interface IMaterialRepository
{
    Task<List<Material>> GetAllAsync(Guid userId);
    Task<Material?> GetByIdAsync(Guid materialId, Guid userId);
    Task AddAsync(Material material);
    Task<bool> DeleteAsync(Guid materialId, Guid userId);
    Task<int> CountByUserIdAsync(Guid userId);
    Task SaveChangesAsync();
}