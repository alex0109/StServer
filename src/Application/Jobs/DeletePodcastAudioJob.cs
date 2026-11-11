using Application.Interfaces;
using Application.Podcasts;

namespace Application.Jobs;

// After an account is deleted: every recorded podcast of it leaves the file storage too
public class DeletePodcastAudioJob
{
    private readonly IAudioStorage _storage;

    public DeletePodcastAudioJob(IAudioStorage storage)
    {
        _storage = storage;
    }

    public async Task Run(Guid userId)
    {
        if (!_storage.IsConfigured) return;
        await _storage.DeletePrefixAsync(PodcastRules.UserPrefix(userId));
    }
}
