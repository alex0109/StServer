using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface IMaterialRepository
{
    Task<List<Material>> GetAllAsync(Guid userId);

    Task<Material?> GetByIdAsync(Guid materialId, Guid userId);
    
    Task<List<Attempt>> GetAttemptsAsync(Guid materialId, Guid userId);

    Task AddAsync(Material material);
    
    Task<bool> DeleteAsync(Guid materialId, Guid userId);

    Task SaveChangesAsync();
}