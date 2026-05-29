namespace StServer.StServer.Application.DTOs;

public class MaterialItemDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string[]? Tags { get; set; }

    public string? Link { get; set; }

    public RichTextDocument? Description { get; set; }

    public string? Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}