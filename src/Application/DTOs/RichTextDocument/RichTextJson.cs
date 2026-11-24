using System.Text.Json;

namespace Application.DTOs.RichTextDocument;

// Editor JSON lives in two shapes: the database keeps what RichTextDocument serializes to by default
// (Type, Content, Attrs...), the editor in the browser reads camelCase (type, content, attrs...).
// Reading accepts both, so content copied around in either shape still opens.
// Every way in and out goes through the sanitizer: text saved before it existed, shared copies and
// library snapshots reach readers clean too
public static class RichTextJson
{
    public static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };

    // Same shape as API answers
    private static readonly JsonSerializerOptions Client = new(JsonSerializerDefaults.Web);

    public static RichTextDocument? Parse(JsonDocument? document) =>
        RichTextSanitizer.Sanitize(document?.Deserialize<RichTextDocument>(Read));

    public static JsonElement ToClient(JsonElement element) =>
        JsonSerializer.SerializeToElement(RichTextSanitizer.Sanitize(element.Deserialize<RichTextDocument>(Read)), Client);

    public static JsonDocument ToStored(JsonElement element) =>
        JsonSerializer.SerializeToDocument(RichTextSanitizer.Sanitize(element.Deserialize<RichTextDocument>(Read)));

    // What the editor sent, cleaned, in the stored shape
    public static JsonDocument ToStored(RichTextDocument document) =>
        JsonSerializer.SerializeToDocument(RichTextSanitizer.Sanitize(document));
}
