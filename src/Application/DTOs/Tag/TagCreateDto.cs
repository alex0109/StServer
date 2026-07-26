using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Tag;

public class TagCreateDto
{
    [MaxLength(50)]
    public required string Name { get; set; }
    public required string Color { get; set; }
}