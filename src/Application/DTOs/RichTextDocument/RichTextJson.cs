using System.Text.Json;

namespace Application.DTOs.RichTextDocument;

// Editor JSON lives in two shapes: the database keeps what RichTextDocument serializes to by default
// (Type, Content, Attrs...), the editor in the browser reads camelCase (type, content, attrs...).
// Reading accepts both, so content copied around in either shape still opens
public static class RichTextJson
{
    public static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };

    // Same shape as API answers
    private static readonly JsonSerializerOptions Client = new(JsonSerializerDefaults.Web);

    public static RichTextDocument? Parse(JsonDocument? document) =>
        document?.Deserialize<RichTextDocument>(Read);

    public static JsonElement ToClient(JsonElement element) =>
        JsonSerializer.SerializeToElement(element.Deserialize<RichTextDocument>(Read), Client);

    public static JsonDocument ToStored(JsonElement element) =>
        JsonSerializer.SerializeToDocument(element.Deserialize<RichTextDocument>(Read));
}
