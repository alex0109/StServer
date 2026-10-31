using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Exceptions;

namespace Application.Ai.Prompts;

// Helpers for JSON schemas and for reading model answers
public static class AiJson
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Strict object schema: every property required, nothing else allowed.
    // Works for the structured output modes of all providers
    public static JsonObject Object(params (string Name, JsonNode Schema)[] properties)
    {
        var props = new JsonObject();
        foreach (var (name, schema) in properties) props[name] = schema;

        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray(properties.Select(x => (JsonNode)x.Name).ToArray()),
            ["properties"] = props,
        };
    }

    public static JsonObject String() => new() { ["type"] = "string" };

    public static JsonObject Integer() => new() { ["type"] = "integer" };

    public static JsonObject Boolean() => new() { ["type"] = "boolean" };

    public static JsonObject Enum(params string[] values) => new()
    {
        ["type"] = "string",
        ["enum"] = new JsonArray(values.Select(x => (JsonNode)x).ToArray()),
    };

    public static JsonObject Array(JsonNode items) => new() { ["type"] = "array", ["items"] = items };

    public static JsonElement ToElement(JsonNode node) => JsonSerializer.SerializeToElement(node);

    public static T Read<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(StripFences(json), ReadOptions)
                   ?? throw new AiOutputException("empty answer");
        }
        catch (JsonException)
        {
            throw new AiOutputException("answer is not valid JSON");
        }
    }

    // Some providers wrap JSON in markdown fences even when asked not to
    private static string StripFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;

        var firstLineEnd = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);

        return firstLineEnd > 0 && lastFence > firstLineEnd
            ? trimmed[(firstLineEnd + 1)..lastFence].Trim()
            : trimmed;
    }

    public static string Limit(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Length <= maxLength ? value.Trim() : value.Trim()[..maxLength];
}
