using Application.DTOs.Ai;

namespace Application.Interfaces;

public interface IWordExplanationService
{
    // Null when the word is not in one of the user's dictionaries
    Task<WordExplanationDto?> ExplainAsync(Guid entryId, CancellationToken cancellationToken = default);
}
