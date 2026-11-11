namespace Application.Interfaces;

// Private storage of generated audio; files are read through short-lived links
public interface IAudioStorage
{
    bool IsConfigured { get; }

    Task UploadAsync(string path, byte[] content, string contentType, CancellationToken cancellationToken = default);

    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    // Every file under a prefix, e.g. all podcasts of a deleted account
    Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default);

    Uri GetReadUrl(string path, TimeSpan validFor);
}
