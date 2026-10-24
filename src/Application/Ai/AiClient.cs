using System.Collections.Concurrent;
using System.Diagnostics;
using Application.Ai.Prompts;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Ai;

// Failover between providers. A provider that keeps failing is skipped for a while,
// so an outage of one company does not slow down every request
public class AiClient : IAiClient
{
    private const int FailuresToOpenCircuit = 3;
    private static readonly TimeSpan OpenCircuitFor = TimeSpan.FromMinutes(2);

    // Shared between requests: provider name -> failures in a row and when it may be tried again
    private static readonly ConcurrentDictionary<string, (int Failures, DateTime RetryAfter)> Health = new();

    private readonly IReadOnlyList<IAiProvider> _providers;
    private readonly AiOptions _options;
    private readonly ILogger<AiClient> _logger;

    public AiClient(IEnumerable<IAiProvider> providers, AiOptions options, ILogger<AiClient> logger)
    {
        _options = options;
        _logger = logger;

        var order = options.ProviderOrder is { Length: > 0 } ? options.ProviderOrder : AiOptions.DefaultProviderOrder;
        var byName = providers.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        _providers = order
            .Where(byName.ContainsKey)
            .Select(name => byName[name])
            .Where(x => x.IsConfigured)
            .ToList();
    }

    public async Task<AiResult<T>> CompleteAsync<T>(IAiPrompt<T> prompt, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _providers.Count == 0)
            throw new AiUnavailableException("AI features are not available right now");

        var request = prompt.BuildRequest();
        var now = DateTime.UtcNow;

        // Providers with an open circuit go last instead of being dropped, they are better than nothing
        var ordered = _providers
            .OrderBy(x => Health.TryGetValue(x.Name, out var h) && h.RetryAfter > now ? 1 : 0)
            .ToList();

        foreach (var provider in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var timeLimit = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeLimit);

            var watch = Stopwatch.StartNew();

            try
            {
                // WaitAsync also stops waiting for a provider that ignores cancellation
                var completion = await provider.CompleteAsync(request, timeout.Token)
                    .WaitAsync(timeLimit, cancellationToken);
                var value = prompt.Parse(completion.Text);

                Health.TryRemove(provider.Name, out _);

                _logger.LogInformation(
                    "AI {Feature} answered by {Provider} ({Model}) in {Elapsed} ms, tokens {Input}/{Output}",
                    request.Feature, completion.Provider, completion.Model, watch.ElapsedMilliseconds,
                    completion.InputTokens, completion.OutputTokens);

                return new AiResult<T>(value, completion);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested && IsProviderFailure(exception))
            {
                MarkFailure(provider.Name);

                // Only the error kind is logged: never request content, never keys
                _logger.LogWarning(
                    "AI {Feature} failed on {Provider} after {Elapsed} ms: {Error}, trying the next provider",
                    request.Feature, provider.Name, watch.ElapsedMilliseconds, Describe(exception));
            }
        }

        throw new AiUnavailableException("AI service is not responding right now, please try again later");
    }

    // Any provider error moves on to the next provider: a broken SDK update of one company
    // must not take AI down while others work. Programming errors still show up in the log
    private static bool IsProviderFailure(Exception exception) =>
        exception is not (OutOfMemoryException or StackOverflowException);

    private static string Describe(Exception exception) => exception switch
    {
        OperationCanceledException or TimeoutException => "timeout",
        AiOutputException => $"invalid output ({exception.Message})",
        AiProviderException => exception.Message,
        HttpRequestException => "connection error",
        // Unknown errors may quote the request, only the type is logged
        _ => $"unexpected {exception.GetType().Name}"
    };

    private static void MarkFailure(string provider)
    {
        Health.AddOrUpdate(
            provider,
            _ => (1, DateTime.MinValue),
            (_, current) =>
            {
                var failures = current.Failures + 1;
                return failures >= FailuresToOpenCircuit
                    ? (failures, DateTime.UtcNow + OpenCircuitFor)
                    : (failures, current.RetryAfter);
            });
    }

    // For tests: providers start healthy
    internal static void ResetHealth() => Health.Clear();
}
