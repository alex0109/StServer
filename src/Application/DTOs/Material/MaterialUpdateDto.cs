using Domain.Utility.Material;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Material;

public class MaterialUpdateDto
{
    [MaxLength(70)]
    public string? Title { get; set; }
    public MaterialType? Type { get; set; }
    public string? Link { get; set; }
    public RichTextDocument.RichTextDocument? Content { get; set; }
    public MaterialStatus? Status { get; set; }
}