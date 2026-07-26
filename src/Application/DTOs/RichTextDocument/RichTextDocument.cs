using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RichTextDocument;

public class NodeAttrs
{
    public int? Width { get; set; }
    public string? Align { get; set; }
}

public class Node
{
    public required string Type { get; set; }

    [MaxLength(5000)]
    public string? Text { get; set; }

    public NodeAttrs? Attrs { get; set; }

    public List<Node>? Content { get; set; }
}

public class RichTextDocument
{
    public string Type { get; set; } = "doc";

    public List<Node> Content { get; set; } = new();
}