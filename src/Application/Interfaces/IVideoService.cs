using Application.DTOs.Video;

namespace Application.Interfaces;

public interface IVideoService
{
    // Null when the material is not the user's, links no YouTube video, or its subtitles aren't written yet
    Task<MaterialVideoDto?> GetAsync(Guid materialId);

    // The AI watches the linked video and writes its subtitles; null when the material is not the user's
    Task<MaterialVideoDto?> ProcessAsync(Guid materialId, CancellationToken cancellationToken = default);
}
