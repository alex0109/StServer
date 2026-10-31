using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Dictionary;

public class DictionaryUpdateDto
{
    [MaxLength(70)]
    public string? Title { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
}