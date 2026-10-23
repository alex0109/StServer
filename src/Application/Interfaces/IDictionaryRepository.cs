using Domain.Entities;

namespace Application.Interfaces;

public interface IDictionaryRepository
{
    Task<List<Dictionary>> GetAllByMaterialIdAsync(Guid materialId, Guid userId);
    Task<Dictionary?> GetByIdAsync(Guid dictionaryId, Guid userId);
    Task AddAsync(Dictionary dictionary);
    Task<bool> DeleteAsync(Guid dictionaryId, Guid userId);
    Task<DictionaryEntry?> GetEntryByIdAsync(Guid entryId, Guid userId);
    Task<List<DictionaryEntry>> GetEntriesByDictionaryIdAsync(Guid dictionaryId, Guid userId);
    Task AddEntryAsync(DictionaryEntry entry);
    Task<bool> DeleteEntryAsync(Guid entryId, Guid userId);
    Task<int> GetMaxEntryPositionAsync(Guid dictionaryId);
    Task<int> CountByMaterialIdAsync(Guid materialId, Guid userId);
    Task<int> CountEntriesAsync(Guid dictionaryId);

    Task SaveChangesAsync();
}