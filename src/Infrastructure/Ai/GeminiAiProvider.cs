using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Application.Ai;
using Application.Exceptions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai;

// Google Gemini generateContent with JSON schema output
public sealed class GeminiAiProvider : HttpAiProvider
{
    public const string ProviderName = "Gemini";

    private readonly AiProviderSettings _settings;
    private readonly string? _apiKey;

    public GeminiAiProvider(IHttpClientFactory httpClientFactory, IOptions<AiProvidersOptions> options, AiProviderKeys keys)
        : base(httpClientFactory)
    {
        _settings = options.Value.Gemini;
        _apiKey = keys.Gemini;
    }

    public override string Name => ProviderName;

    // Gemini takes public YouTube links as input
    public override bool CanWatchVideo => true;

    public override bool IsConfigured =>
        _apiKey is not null && !string.IsNullOrWhiteSpace(_settings.Model) && _settings.HasSecureBaseUrl;

    public override async Task<AiCompletion> CompleteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new AiProviderException($"{ProviderName} is not configured");

        // The video goes before the text: Gemini reads the request in this order
        var parts = new JsonArray();
        if (request.Video is { } video)
        {
            parts.Add(new JsonObject
            {
                ["fileData"] = new JsonObject { ["fileUri"] = video.Url },
                ["videoMetadata"] = new JsonObject
                {
                    ["startOffset"] = "0s",
                    ["endOffset"] = $"{video.EndSeconds}s",
                    ["fps"] = video.FramesPerSecond,
                },
            });
        }
        parts.Add(new JsonObject { ["text"] = request.UserPrompt });

        var generationConfig = new JsonObject
        {
            ["maxOutputTokens"] = request.MaxOutputTokens,
            ["responseMimeType"] = "application/json",
            ["responseJsonSchema"] = ToNode(request.OutputSchema),
        };
        // Small frames: the words come from the sound, and a frame costs 66 tokens instead of 258
        if (request.Video is not null) generationConfig["mediaResolution"] = "MEDIA_RESOLUTION_LOW";

        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = request.SystemPrompt } },
            },
            ["contents"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["parts"] = parts },
            },
            ["generationConfig"] = generationConfig,
        };

        // The key goes in a header, never in the URL where proxies and logs would keep it
        var model = Uri.EscapeDataString(_settings.Model!);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_settings.BaseUrl!.TrimEnd('/')}/models/{model}:generateContent");
        message.Headers.Add("x-goog-api-key", _apiKey);
        message.Content = JsonContent.Create(body);

        var response = await SendAsync(message, cancellationToken);
        var candidate = response["candidates"]?[0];
        var finishReason = candidate?["finishReason"]?.GetValue<string>();

        if (finishReason == "MAX_TOKENS")
            throw new AiProviderException($"{ProviderName} answer was cut off");
        if (candidate is null || finishReason is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "RECITATION")
            throw new AiProviderException($"{ProviderName} declined the request");

        var text = new StringBuilder();
        foreach (var part in candidate["content"]?["parts"]?.AsArray() ?? [])
        {
            // Thought parts are reasoning, only the answer is kept
            if (part?["thought"]?.GetValue<bool>() == true) continue;
            text.Append(part?["text"]?.GetValue<string>());
        }

        if (text.Length == 0) throw new AiProviderException($"{ProviderName} returned no text");

        return new AiCompletion
        {
            Text = text.ToString(),
            Provider = ProviderName,
            Model = response["modelVersion"]?.GetValue<string>() ?? _settings.Model!,
            InputTokens = response["usageMetadata"]?["promptTokenCount"]?.GetValue<long>() ?? 0,
            OutputTokens = response["usageMetadata"]?["candidatesTokenCount"]?.GetValue<long>() ?? 0,
        };
    }
}
