using Domain.Entities;

namespace Application.Interfaces;

public interface INoteRepository
{
    Task<List<Note>> GetAllAsync(Guid materialId, Guid userId);
    Task<Note?> GetByIdAsync(Guid noteId, Guid materialId, Guid userId);
    Task AddAsync(Note note);
    Task<bool> DeleteAsync(Guid noteId, Guid materialId, Guid userId);
    Task<int> CountByUserIdAsync(Guid materialId, Guid userId);
    Task SaveChangesAsync();
}