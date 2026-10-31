using System.Text;
using System.Text.Json;
using Application.DTOs.RichTextDocument;

namespace Application.Ai.Text;

// Converts the editor's document format to plain text for AI, and AI summaries back to it
public static class RichTextConverter
{
    private const int MaxNodeText = 5000;

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly HashSet<string> BlockTypes =
        ["paragraph", "heading", "blockquote", "codeBlock", "bulletList", "orderedList"];

    public static string ToPlainText(JsonDocument? document)
    {
        if (document is null) return string.Empty;

        try
        {
            return ToPlainText(document.Deserialize<RichTextDocument>(ReadOptions));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    public static string ToPlainText(RichTextDocument? document)
    {
        var builder = new StringBuilder();

        foreach (var node in document?.Content ?? [])
        {
            Append(node, builder);
        }

        return builder.ToString().Trim();
    }

    private static void Append(Node node, StringBuilder builder)
    {
        switch (node.Type)
        {
            case "text":
                builder.Append(node.Text);
                return;
            case "hardBreak":
                builder.Append('\n');
                return;
            case "listItem":
                builder.Append("- ");
                break;
        }

        foreach (var child in node.Content ?? [])
        {
            Append(child, builder);
        }

        if (BlockTypes.Contains(node.Type)) builder.Append('\n');
    }

    public static RichTextDocument FromSections(IEnumerable<SummarySection> sections)
    {
        var document = new RichTextDocument();

        foreach (var section in sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Heading))
            {
                document.Content.Add(new Node
                {
                    Type = "heading",
                    Attrs = new Dictionary<string, object?> { ["level"] = 2 },
                    Content = [TextNode(section.Heading)],
                });
            }

            foreach (var paragraph in section.Paragraphs.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                document.Content.Add(new Node { Type = "paragraph", Content = [TextNode(paragraph)] });
            }

            var bullets = section.Bullets.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (bullets.Count > 0)
            {
                document.Content.Add(new Node
                {
                    Type = "bulletList",
                    Content = bullets
                        .Select(x => new Node
                        {
                            Type = "listItem",
                            Content = [new Node { Type = "paragraph", Content = [TextNode(x)] }],
                        })
                        .ToList(),
                });
            }
        }

        return document;
    }

    private static Node TextNode(string text) => new()
    {
        Type = "text",
        Text = text.Length <= MaxNodeText ? text : text[..MaxNodeText],
    };
}

public record SummarySection(string Heading, IReadOnlyList<string> Paragraphs, IReadOnlyList<string> Bullets);
