namespace StServer.Application.DTOs.Material;

public class MaterialResponseDto
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Type { get; set; }
    public string[]? Tags { get; set; }
    public string? Link { get; set; }
    public RichTextDocument? Description { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}