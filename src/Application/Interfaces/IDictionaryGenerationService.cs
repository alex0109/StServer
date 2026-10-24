using Application.DTOs.Ai;
using Application.DTOs.Dictionary;

namespace Application.Interfaces;

public interface IDictionaryGenerationService
{
    Task<List<DictionaryEntryResponseDto>?> GenerateAsync(Guid dictionaryId, DictionaryEntriesGenerateDto dto, CancellationToken cancellationToken = default);
}
