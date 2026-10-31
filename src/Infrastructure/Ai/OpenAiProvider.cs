using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Application.Ai;
using Application.Exceptions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai;

// OpenAI Chat Completions with strict JSON schema output
public sealed class OpenAiProvider : HttpAiProvider
{
    public const string ProviderName = "OpenAi";

    private readonly AiProviderSettings _settings;
    private readonly string? _apiKey;

    public OpenAiProvider(IHttpClientFactory httpClientFactory, IOptions<AiProvidersOptions> options, AiProviderKeys keys)
        : base(httpClientFactory)
    {
        _settings = options.Value.OpenAi;
        _apiKey = keys.OpenAi;
    }

    public override string Name => ProviderName;

    // The model has no default: OpenAI renames models often, it must be chosen in configuration
    public override bool IsConfigured =>
        _apiKey is not null && !string.IsNullOrWhiteSpace(_settings.Model) && _settings.HasSecureBaseUrl;

    public override async Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new AiProviderException($"{ProviderName} is not configured");

        var body = new JsonObject
        {
            ["model"] = _settings.Model,
            ["max_completion_tokens"] = request.MaxOutputTokens,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = request.UserPrompt },
            },
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = "answer",
                    ["strict"] = true,
                    ["schema"] = ToNode(request.OutputSchema),
                },
            },
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl!.TrimEnd('/')}/chat/completions");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        message.Content = JsonContent.Create(body);

        var response = await SendAsync(message, cancellationToken);
        var choice = response["choices"]?[0];

        if (choice?["message"]?["refusal"] is JsonValue)
            throw new AiProviderException($"{ProviderName} declined the request");
        if (choice?["finish_reason"]?.GetValue<string>() == "length")
            throw new AiProviderException($"{ProviderName} answer was cut off");

        var text = choice?["message"]?["content"]?.GetValue<string>();
        if (string.IsNullOrEmpty(text)) throw new AiProviderException($"{ProviderName} returned no text");

        return new AiCompletion
        {
            Text = text,
            Provider = ProviderName,
            Model = response["model"]?.GetValue<string>() ?? _settings.Model!,
            InputTokens = response["usage"]?["prompt_tokens"]?.GetValue<long>() ?? 0,
            OutputTokens = response["usage"]?["completion_tokens"]?.GetValue<long>() ?? 0,
        };
    }
}
