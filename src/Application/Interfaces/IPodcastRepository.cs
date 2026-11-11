using Domain.Entities;

namespace Application.Interfaces;

public interface IPodcastRepository
{
    Task<MaterialPodcast?> GetByMaterialAsync(Guid materialId, Guid userId);
    Task<MaterialPodcast?> GetByIdAsync(Guid podcastId);
    Task<List<string>> GetAudioPathsAsync(Guid userId);
    Task AddAsync(MaterialPodcast podcast);
    Task AddSpeechUsageAsync(SpeechUsage usage);
    void Remove(MaterialPodcast podcast);
    Task SaveChangesAsync();
}
