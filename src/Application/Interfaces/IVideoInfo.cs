namespace Application.Interfaces;

// Public facts about a YouTube video, from YouTube itself
public interface IVideoInfo
{
    // Null when YouTube doesn't answer or the video is private or gone
    Task<string?> GetTitleAsync(string videoId, CancellationToken cancellationToken = default);
}
