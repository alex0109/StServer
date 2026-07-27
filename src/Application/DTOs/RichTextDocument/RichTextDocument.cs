using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RichTextDocument;

public class Mark
{
    public string? Type { get; set; }
    public Dictionary<string, object?>? Attrs { get; set; }
}

public class Node
{
    public required string Type { get; set; }

    [MaxLength(5000)]
    public string? Text { get; set; }

    public Mark[]? Marks { get; set; }

    public Dictionary<string, object?>? Attrs { get; set; }

    public List<Node>? Content { get; set; }
}

public class RichTextDocument
{
    public string Type { get; set; } = "doc";

    public List<Node> Content { get; set; } = new();
}