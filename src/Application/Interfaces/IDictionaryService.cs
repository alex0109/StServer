using Application.DTOs.Dictionary;
using Application.DTOs.Question;

namespace Application.Interfaces;

public interface IDictionaryService
{
    Task<List<DictionaryResponseDto>> GetAllByMaterialIdAsync(Guid materialId);

    Task<DictionaryResponseDto?> GetByIdAsync(Guid dictionaryId);

    Task<DictionaryResponseDto?> CreateAsync(Guid materialId, DictionaryCreateDto dto);

    Task<DictionaryResponseDto?> UpdateAsync(Guid dictionaryId, DictionaryUpdateDto dto);

    Task<bool> DeleteAsync(Guid dictionaryId);

    Task<DictionaryEntryResponseDto?> CreateEntryAsync(Guid dictionaryId, DictionaryEntryCreateDto dto);

    Task<DictionaryEntryResponseDto?> GetEntryByIdAsync(Guid entryId);

    Task<DictionaryEntryResponseDto?> UpdateEntryAsync(Guid entryId, DictionaryEntryUpdateDto dto);

    Task<bool> DeleteEntryAsync(Guid entryId);

    Task<List<DictionaryEntryResponseDto>?> ReorderEntriesAsync(Guid dictionaryId, DictionaryEntriesReorderDto dto);

    Task<List<QuestionResponseDto>?> GetQuestionsAsync(Guid dictionaryId);
}