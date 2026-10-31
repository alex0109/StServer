using Application.DTOs.Ai;

namespace Application.Interfaces;

public interface IWordLookupService
{
    // Null when the dictionary is not the user's. Never creates a word
    Task<WordLookupDto?> LookupAsync(Guid dictionaryId, WordLookupRequestDto dto, CancellationToken cancellationToken = default);
}
