using System.Text.Json;

namespace Infrastructure.Ai;

// Reason a provider gives for a rejected request ("credit balance is too low", "model not found").
// Only the error message is taken, never the rest of the body, and it is kept short for logs
internal static class AiProviderErrors
{
    private const int MaxLength = 300;

    public static string Describe(string provider, int statusCode, string? body)
    {
        var reason = ReadMessage(body);
        return reason is null
            ? $"{provider} API error {statusCode}"
            : $"{provider} API error {statusCode}: {reason}";
    }

    private static string? ReadMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        // The SDK puts the status line before the JSON body
        var start = body.IndexOf('{');
        if (start < 0) return null;

        try
        {
            using var json = JsonDocument.Parse(body[start..]);

            // Anthropic, OpenAI and Gemini all answer with { "error": { "message": ... } }
            if (json.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                var text = message.GetString()!.ReplaceLineEndings(" ").Trim();
                return text.Length <= MaxLength ? text : text[..MaxLength];
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
