using Application.Ai;
using Application.Ai.Prompts;
using Application.Exceptions;
using Application.Interfaces;
using Application.Observability;

namespace Application.Services.Ai;

// Every AI feature runs through here: the usage gate reserves its units, the client asks the
// providers, and the reservation is committed with what it cost or released when nothing came of it
public class AiService : IAiService
{
    // Supabase marks sessions of anonymous sign-ins; they cost nothing to create, so free quotas and
    // trials on them would never run out
    public const string AnonymousClaim = "is_anonymous";

    private readonly IAiClient _client;
    private readonly IAiUsageService _usage;
    private readonly IUserContext _user;

    public AiService(IAiClient client, IAiUsageService usage, IUserContext user)
    {
        _client = client;
        _usage = usage;
        _user = user;
    }

    public Task<AiResult<T>> RunAsync<T>(IAiPrompt<T> prompt, CancellationToken cancellationToken = default) =>
        RunAsync(prompt, _ => true, string.Empty, cancellationToken);

    public Task<AiResult<T>> RunAsync<T>(
        IAiPrompt<T> prompt,
        Func<T, bool> isUsable,
        string emptyMessage,
        CancellationToken cancellationToken = default) =>
        RunAsync(prompt, new AiUnits<T>(1), isUsable, emptyMessage, cancellationToken);

    public async Task<AiResult<T>> RunAsync<T>(
        IAiPrompt<T> prompt,
        AiUnits<T> units,
        Func<T, bool> isUsable,
        string emptyMessage,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(_user.Claim(AnonymousClaim), "true", StringComparison.OrdinalIgnoreCase))
            throw new AiUsageDeniedException(AiDenialCode.AccountRequired, prompt.Feature, "AI features need an account with an email");

        var reservation = await _usage.ReserveAsync(_user.UserId, prompt.Feature, units.Reserved, units.AllowCredits);

        // Once a provider has the request it bills it, whether the client still waits or not: leaving
        // the page must not make it free. Only suggestions made while typing stop with the client
        var providerToken = units.StopsWithClient ? cancellationToken : CancellationToken.None;

        AiResult<T> result;
        try
        {
            result = await _client.CompleteAsync(prompt, providerToken);
        }
        catch (AiOutputException exception) when (exception.Completion is { } unreadable)
        {
            // An answer came back, just not in the asked shape: it was paid for
            AppMetrics.AiTokensUsed(prompt.Feature.ToString(), unreadable.Provider, unreadable.InputTokens, unreadable.OutputTokens);
            await _usage.SettleUnusableAsync(reservation.Id, unreadable);
            AppMetrics.AiRequest(prompt.Feature.ToString(), unreadable.Provider, "invalid");
            throw;
        }
        catch
        {
            // No provider answered, so the request takes nothing
            await _usage.ReleaseAsync(reservation.Id);
            AppMetrics.AiRequest(prompt.Feature.ToString(), "none", "failed");
            throw;
        }

        AppMetrics.AiTokensUsed(prompt.Feature.ToString(), result.Completion.Provider, result.Completion.InputTokens, result.Completion.OutputTokens);

        // The model answered, but with nothing the user gets: forgiven a few times a day, then counted
        if (!isUsable(result.Value))
        {
            await _usage.SettleUnusableAsync(reservation.Id, result.Completion);
            AppMetrics.AiRequest(prompt.Feature.ToString(), result.Completion.Provider, "empty");
            throw new AiOutputException(emptyMessage);
        }

        await _usage.CommitAsync(reservation.Id, result.Completion, units.Used?.Invoke(result.Value), units.VideoSeconds?.Invoke(result.Value));
        AppMetrics.AiRequest(prompt.Feature.ToString(), result.Completion.Provider, "succeeded");

        return result;
    }

    public async Task<AiResult<T>?> TryRunAsync<T>(IAiPrompt<T> prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            return await RunAsync(prompt, cancellationToken);
        }
        catch (Exception exception) when (exception is AiUsageDeniedException or AiUnavailableException)
        {
            return null;
        }
    }
}
