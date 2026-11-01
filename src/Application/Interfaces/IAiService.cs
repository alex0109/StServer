using Application.Ai;
using Application.Ai.Prompts;

namespace Application.Interfaces;

// Entry point of AI features: quota of the current user, then the provider failover
public interface IAiService
{
    Task<AiResult<T>> RunAsync<T>(IAiPrompt<T> prompt, CancellationToken cancellationToken = default);

    // For answers that parse but may hold nothing to use (no new words, no questions): then the
    // request goes back to the quota and AiOutputException(emptyMessage) is thrown
    Task<AiResult<T>> RunAsync<T>(IAiPrompt<T> prompt, Func<T, bool> isUsable, string emptyMessage, CancellationToken cancellationToken = default);

    // For optional AI steps: null when the quota is used up or no provider answers
    Task<AiResult<T>?> TryRunAsync<T>(IAiPrompt<T> prompt, CancellationToken cancellationToken = default);
}
