using Application.DTOs.Ai;

namespace Application.Interfaces;

public interface ISummaryGenerationService
{
    Task<SummaryGenerateResponseDto?> GenerateAsync(Guid materialId, Guid noteId, CancellationToken cancellationToken = default);
}
