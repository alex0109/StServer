using Application.Ai;
using Application.Ai.Prompts;

namespace Application.Interfaces;

public interface IAiClient
{
    // Tries providers in the configured order, throws AiUnavailableException when none answers
    Task<AiResult<T>> CompleteAsync<T>(IAiPrompt<T> prompt, CancellationToken cancellationToken = default);
}
