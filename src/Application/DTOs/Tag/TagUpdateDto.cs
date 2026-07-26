using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Tag;

public class TagUpdateDto
{
    [MaxLength(50)]
    public string? Name { get; set; }
    public string? Color { get; set; }
}