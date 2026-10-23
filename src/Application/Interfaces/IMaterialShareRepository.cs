using Domain.Entities;

namespace Application.Interfaces;

public interface IMaterialShareRepository
{
    Task<MaterialShare?> GetActiveByMaterialIdAsync(Guid materialId, Guid userId);
    // Loads the whole material: notes, questions with options, dictionaries with entries
    Task<MaterialShare?> GetActiveByTokenAsync(string token);
    Task<bool> HasImportedAsync(Guid sourceMaterialId, Guid userId);
    Task AddAsync(MaterialShare share);

    Task SaveChangesAsync();
}
