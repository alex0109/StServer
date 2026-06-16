using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface IMaterialRepository
{
    Task<List<Material>> GetAllAsync();

    Task<Material?> GetByIdAsync(Guid id);

    Task<Material> AddAsync(Material material);
    
    Task<bool> DeleteAsync(Guid id);

    Task SaveChangesAsync();
}