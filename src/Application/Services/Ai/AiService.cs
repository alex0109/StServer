using Application.Ai;
using Application.Ai.Prompts;
using Application.Exceptions;
using Application.Interfaces;
using Application.Observability;

namespace Application.Services.Ai;

public class AiService : IAiService
{
    private readonly IAiClient _client;
    private readonly IAiUsageService _usage;
    private readonly IUserContext _user;

    public AiService(IAiClient client, IAiUsageService usage, IUserContext user)
    {
        _client = client;
        _usage = usage;
        _user = user;
    }

    public async Task<AiResult<T>> RunAsync<T>(IAiPrompt<T> prompt, CancellationToken cancellationToken = default)
    {
        var usageId = await _usage.ReserveAsync(_user.UserId, prompt.Feature);

        AiResult<T> result;
        try
        {
            result = await _client.CompleteAsync(prompt, cancellationToken);
        }
        catch
        {
            // Nothing was produced, so the request does not count
            await _usage.FailAsync(usageId);
            AppMetrics.AiRequest(prompt.Feature.ToString(), "none", "failed");
            throw;
        }

        await _usage.CompleteAsync(usageId, result.Completion);
        AppMetrics.AiRequest(prompt.Feature.ToString(), result.Completion.Provider, "succeeded");
        AppMetrics.AiTokensUsed(prompt.Feature.ToString(), result.Completion.Provider, result.Completion.InputTokens, result.Completion.OutputTokens);

        return result;
    }

    public async Task<AiResult<T>?> TryRunAsync<T>(IAiPrompt<T> prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunAsync(prompt, cancellationToken);
        }
        catch (Exception exception) when (exception is AiQuotaExceededException or AiUnavailableException)
        {
            return null;
        }
    }
}
