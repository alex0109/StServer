using Application.DTOs.Podcast;

namespace Application.Interfaces;

public interface IPodcastService
{
    // Null when the material is not the user's or has no podcast yet
    Task<PodcastDto?> GetAsync(Guid materialId);

    // Writes the script with AI and starts recording; null when the material is not the user's
    Task<PodcastDto?> GenerateAsync(Guid materialId, PodcastGenerateDto dto, CancellationToken cancellationToken = default);

    // Records the saved script again after a failure; no AI request, so no quota
    Task<PodcastDto?> RetryAsync(Guid materialId);
}
