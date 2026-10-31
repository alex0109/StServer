using Application.Ai;

namespace Application.Interfaces;

// One AI company behind a common contract. Implementations live in Infrastructure,
// they only transport requests and must throw AiProviderException on any failure
public interface IAiProvider
{
    string Name { get; }

    // False when the key or model is not configured, such a provider is skipped
    bool IsConfigured { get; }

    Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken);
}
