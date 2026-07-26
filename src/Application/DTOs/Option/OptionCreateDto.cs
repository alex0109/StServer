using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Option;

public class OptionCreateDto
{
    [MaxLength(500)]
    public required string Name { get; set; }
    public bool IsCorrect { get; set; }
}