using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Dictionary;

public class DictionaryCreateDto
{
    [MaxLength(70)]
    public required string Title { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
}