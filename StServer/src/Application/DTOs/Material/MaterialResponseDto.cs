using StServer.Application.DTOs.Tag;
using StServer.Domain.Utility.Material;

namespace StServer.Application.DTOs.Material;

public class MaterialResponseDto
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required MaterialType Type { get; set; }
    public List<TagResponseDto>? MaterialTags { get; set; }
    public string? Link { get; set; }
    public RichTextDocument? Content { get; set; }
    public required MaterialStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}