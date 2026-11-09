using Application.DTOs.Dictionary;

namespace Application.Interfaces;

public interface IWordQuizService
{
    // Null when the user has no such word
    Task<WordQuizDto?> GetQuizAsync(Guid entryId);
}
