namespace StServer.Application.DTOs.Material;

public class MaterialCreateDto
{
    public string Title { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string[]? Tags { get; set; }
    public string? Link { get; set; }
    public RichTextDocument? Description { get; set; }
}