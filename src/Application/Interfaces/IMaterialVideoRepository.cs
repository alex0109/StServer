using Domain.Entities;

namespace Application.Interfaces;

public interface IMaterialVideoRepository
{
    Task<MaterialVideo?> GetByMaterialAsync(Guid materialId, Guid userId);
    Task AddAsync(MaterialVideo video);
    Task SaveChangesAsync();
}
