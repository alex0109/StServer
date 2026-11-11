using Application.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Infrastructure.Podcasts;

// A private container; the browser plays a file through a read-only link that expires
public class AzureBlobAudioStorage : IAudioStorage
{
    private readonly BlobContainerClient? _container;
    private bool _containerReady;

    public AzureBlobAudioStorage(string? connectionString, string containerName)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
            _container = new BlobContainerClient(connectionString, containerName);
    }

    public static AzureBlobAudioStorage FromEnvironment() => new(
        Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING"),
        Environment.GetEnvironmentVariable("PODCASTS_CONTAINER") is { Length: > 0 } name ? name : "podcasts");

    public bool IsConfigured => _container is not null;

    public async Task UploadAsync(string path, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        var container = await ContainerAsync(cancellationToken);
        await container.GetBlobClient(path).UploadAsync(
            new BinaryData(content),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = "private, max-age=86400" },
            },
            cancellationToken);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var container = await ContainerAsync(cancellationToken);
        await container.GetBlobClient(path).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var container = await ContainerAsync(cancellationToken);
        await foreach (var blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken))
        {
            await container.GetBlobClient(blob.Name).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }

    public Uri GetReadUrl(string path, TimeSpan validFor)
    {
        var blob = Container.GetBlobClient(path);
        // A little early start covers clocks that run behind
        return blob.GenerateSasUri(new BlobSasBuilder(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(validFor))
        {
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
        });
    }

    private BlobContainerClient Container =>
        _container ?? throw new InvalidOperationException("Audio storage is not set up");

    private async Task<BlobContainerClient> ContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerReady) return Container;
        await Container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        _containerReady = true;
        return Container;
    }
}
