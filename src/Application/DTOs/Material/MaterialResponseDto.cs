using Application.DTOs.Tag;
using Domain.Utility.Material;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Material;

public class MaterialResponseDto
{
    public Guid Id { get; set; }
    public Guid? AssessmentId { get; set; }
    [MaxLength(70)]
    public required string Title { get; set; }
    public required MaterialType Type { get; set; }
    public List<TagResponseDto>? MaterialTags { get; set; }
    public string? Link { get; set; }
    public RichTextDocument.RichTextDocument? Content { get; set; }
    public required MaterialStatus Status { get; set; }
    public required bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}