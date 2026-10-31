using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Logging;
using Application.Ai;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai;

public sealed class AnthropicAiProvider : IAiProvider
{
    public const string ProviderName = "Anthropic";

    // A request the model declines for policy reasons is re-served by the model Anthropic recommends
    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    private readonly AnthropicClient? _client;
    private readonly string? _model;
    private readonly ILogger<AnthropicAiProvider> _logger;

    // What the configured model accepts, read once from the Models API
    private readonly SemaphoreSlim _featuresLock = new(1, 1);
    private ModelFeatures? _features;

    public AnthropicAiProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiProvidersOptions> options,
        AiProviderKeys keys,
        ILogger<AnthropicAiProvider> logger)
    {
        var settings = options.Value.Anthropic;
        _model = string.IsNullOrWhiteSpace(settings.Model) ? null : settings.Model.Trim();
        _logger = logger;

        if (keys.Anthropic is null || _model is null) return;
        if (!string.IsNullOrWhiteSpace(settings.BaseUrl) && !settings.HasSecureBaseUrl) return;

        // Retries are done by AiClient across providers, one SDK retry covers short network blips
        var httpClient = httpClientFactory.CreateClient(ProviderName);
        _client = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? new AnthropicClient { ApiKey = keys.Anthropic, HttpClient = httpClient, MaxRetries = 1 }
            : new AnthropicClient { ApiKey = keys.Anthropic, HttpClient = httpClient, MaxRetries = 1, BaseUrl = settings.BaseUrl };
    }

    public string Name => ProviderName;

    public bool IsConfigured => _client is not null;

    public async Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        if (_client is null || _model is null) throw new AiProviderException($"{ProviderName} is not configured");

        var features = await GetFeaturesAsync(cancellationToken);
        if (!features.StructuredOutputs)
            throw new AiProviderException($"{ProviderName} model {_model} has no structured outputs");

        // Only what the configured model supports is sent, so changing the model needs no code change
        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = request.MaxOutputTokens,
            System = request.SystemPrompt,
            Messages = [new BetaUserMessageParam { Content = request.UserPrompt }],
            OutputConfig = features.Effort
                ? new BetaOutputConfig
                {
                    Effort = request.Effort switch
                    {
                        AiEffort.Low => Effort.Low,
                        AiEffort.High => Effort.High,
                        _ => Effort.Medium,
                    },
                    Format = new BetaJsonOutputFormat { Schema = ToSchema(request.OutputSchema) },
                }
                : new BetaOutputConfig
                {
                    Format = new BetaJsonOutputFormat { Schema = ToSchema(request.OutputSchema) },
                },
        };

        if (features.RefusalFallbacks)
        {
            parameters = parameters with { Fallbacks = new Default(), Betas = [FallbackBeta] };
        }

        BetaMessage message;
        try
        {
            message = await _client.Beta.Messages.Create(parameters, cancellationToken);
        }
        catch (AnthropicApiException exception)
        {
            // Only the API's own reason goes further, not the rest of the exception
            throw new AiProviderException(
                AiProviderErrors.Describe(ProviderName, (int)exception.StatusCode, exception.Message),
                exception);
        }
        catch (AnthropicIOException exception)
        {
            throw new AiProviderException($"{ProviderName} connection error", exception);
        }

        if (message.StopReason == "refusal")
            throw new AiProviderException($"{ProviderName} declined the request");
        if (message.StopReason == "max_tokens")
            throw new AiProviderException($"{ProviderName} answer was cut off");

        var text = new StringBuilder();
        foreach (var block in message.Content)
        {
            if (block.TryPickText(out var textBlock)) text.Append(textBlock.Text);
        }

        if (text.Length == 0) throw new AiProviderException($"{ProviderName} returned no text");

        return new AiCompletion
        {
            Text = text.ToString(),
            Provider = ProviderName,
            Model = message.Model,
            InputTokens = message.Usage.InputTokens,
            OutputTokens = message.Usage.OutputTokens,
        };
    }

    private async Task<ModelFeatures> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        if (_features is not null) return _features;

        await _featuresLock.WaitAsync(cancellationToken);
        try
        {
            if (_features is not null) return _features;

            // The fallback beta adds the list of models the configured one can fall back to
            var info = await _client!.Beta.Models.Retrieve(
                _model!,
                new Anthropic.Models.Beta.Models.ModelRetrieveParams { Betas = [FallbackBeta] },
                cancellationToken);

            _features = new ModelFeatures(
                StructuredOutputs: info.Capabilities?.StructuredOutputs?.Supported ?? true,
                Effort: info.Capabilities?.Effort?.Supported ?? true,
                RefusalFallbacks: info.AllowedFallbackModels is { Count: > 0 });

            _logger.LogInformation(
                "Anthropic model {Model}: effort {Effort}, refusal fallbacks {Fallbacks}",
                _model, _features.Effort, _features.RefusalFallbacks);

            return _features;
        }
        catch (AnthropicApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new AiProviderException($"{ProviderName} model {_model} does not exist");
        }
        catch (Exception exception) when (exception is AnthropicException or HttpRequestException)
        {
            // Models API is down: the usual features of current models, not remembered, so the next call checks again
            _logger.LogWarning("Could not read features of Anthropic model {Model}, using defaults", _model);
            return new ModelFeatures(StructuredOutputs: true, Effort: true, RefusalFallbacks: false);
        }
        finally
        {
            _featuresLock.Release();
        }
    }

    private sealed record ModelFeatures(bool StructuredOutputs, bool Effort, bool RefusalFallbacks);

    private static Dictionary<string, JsonElement> ToSchema(JsonElement schema) =>
        schema.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone());
}
