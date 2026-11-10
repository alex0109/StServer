using Application.DTOs.Ai;

namespace Application.Interfaces;

public interface IEssayReviewService
{
    // Null when the note is not the user's
    Task<EssayReviewResponseDto?> ReviewAsync(Guid materialId, Guid noteId, CancellationToken cancellationToken = default);
}
