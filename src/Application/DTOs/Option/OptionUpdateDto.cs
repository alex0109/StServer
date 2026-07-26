using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Option;

public class OptionUpdateDto
{
    public Guid? Id { get; set; }
    [MaxLength(500)]
    public string? Name { get; set; }
    public bool? IsCorrect { get; set; }
}