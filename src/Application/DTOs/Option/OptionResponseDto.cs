using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Option;

public class OptionResponseDto
{
    public Guid Id { get; set; }
    [MaxLength(500)]
    public required string Name { get; set; }
    public bool IsCorrect { get; set; }
}