using Application.WordQuiz;
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
    // Case-insensitive, so "Hund" and "hund" are one word
    Task<Guid?> FindEntryIdByTermAsync(Guid dictionaryId, string term);
    // Other words of the user in a random order, up to perSource of them from the word's own dictionary
    // first, then from the user's other dictionaries
    Task<List<WordQuizCandidate>> GetQuizCandidatesAsync(Guid entryId, Guid dictionaryId, Guid userId, int perSource);

    Task SaveChangesAsync();
}