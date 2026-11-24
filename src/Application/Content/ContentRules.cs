using System.Text.Json;
using Application.Exceptions;
using RichText = Application.DTOs.RichTextDocument;

namespace Application.Content;

// How much one note or material may hold, checked before anything is saved. Without it a single
// account could post documents of tens of megabytes and fill the database. The caps are far above
// what a person writes or draws; the request size limits of the API stand in front of them
public static class ContentRules
{
    public const int MaxMaterialTitleLength = 70;
    public const int MaxNoteTitleLength = 200;

    // Editor JSON of one material or note: a long article is a few hundred thousand characters
    public const int MaxRichTextChars = 2_000_000;
    public const int MaxRichTextNodes = 50_000;
    public const int MaxRichTextDepth = 40;

    // Strokes of one handwritten note
    public const int MaxDrawingChars = 8_000_000;

    // A title of null means "not changed"
    public static void EnsureTitle(string? title, int maxLength, string what)
    {
        if (title is not null && title.Length > maxLength)
            throw new BadRequestException($"{what} title is longer than {maxLength} characters");
    }

    // The document's size as JSON, for the storage quota; 0 when there is none
    public static int EnsureRichText(RichText.RichTextDocument? document, string what)
    {
        if (document is null) return 0;

        var nodes = 0;
        CountNodes(document.Content, 1, ref nodes, what);

        var size = JsonSerializer.Serialize(document).Length;
        if (size > MaxRichTextChars)
            throw new BadRequestException($"{what} is too large: at most {MaxRichTextChars} characters of editor data");

        return size;
    }

    public static int EnsureDrawing(JsonDocument? drawing)
    {
        if (drawing is null || drawing.RootElement.ValueKind == JsonValueKind.Null) return 0;

        var size = drawing.RootElement.GetRawText().Length;
        if (size > MaxDrawingChars)
            throw new BadRequestException($"The drawing is too large: at most {MaxDrawingChars} characters");

        return size;
    }

    private static void CountNodes(List<RichText.Node>? nodes, int depth, ref int count, string what)
    {
        if (nodes is null) return;

        if (depth > MaxRichTextDepth)
            throw new BadRequestException($"{what} is nested too deeply");

        foreach (var node in nodes)
        {
            if (++count > MaxRichTextNodes)
                throw new BadRequestException($"{what} has too many parts: at most {MaxRichTextNodes}");

            CountNodes(node?.Content, depth + 1, ref count, what);
        }
    }
}
