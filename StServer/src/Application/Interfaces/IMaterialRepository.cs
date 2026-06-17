using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface IMaterialRepository
{
    Task<List<Material>> GetAllAsync(Guid userId);

    Task<Material?> GetByIdAsync(Guid id, Guid userId);

    Task<Material> AddAsync(Material material);
    
    Task<bool> DeleteAsync(Guid id, Guid userId);

    Task SaveChangesAsync();
}