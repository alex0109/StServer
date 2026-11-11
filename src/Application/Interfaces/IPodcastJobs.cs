namespace Application.Interfaces;

// Recording the voices takes minutes, so it runs in the background
public interface IPodcastJobs
{
    void RecordAudio(Guid podcastId);
}
