using StServer.Models;

namespace StServer.DTOs;

public class MaterialItemDto
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string[]? Tags { get; set; }

    public string? Link { get; set; }

    public RichTextDocument? Description { get; set; }

    public string? Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}