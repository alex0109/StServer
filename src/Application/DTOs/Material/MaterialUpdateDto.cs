using Domain.Utility.Material;

namespace Application.DTOs.Material;

public class MaterialUpdateDto
{
    public string? Title { get; set; }
    public MaterialType? Type { get; set; }
    public string? Link { get; set; }
    public RichTextDocument? Content { get; set; }
    public MaterialStatus? Status { get; set; }
}