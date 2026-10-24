using Application.Ai;
using Application.Ai.Prompts;

namespace Application.Interfaces;

// Entry point of AI features: quota of the current user, then the provider failover
public interface IAiService
{
    Task<AiResult<T>> RunAsync<T>(IAiPrompt<T> prompt, CancellationToken cancellationToken = default);

    // For optional AI steps: null when the quota is used up or no provider answers
    Task<AiResult<T>?> TryRunAsync<T>(IAiPrompt<T> prompt, CancellationToken cancellationToken = default);
}
