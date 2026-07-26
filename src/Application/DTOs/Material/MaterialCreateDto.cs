using Domain.Utility.Material;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Material;

public class MaterialCreateDto
{
    [MaxLength(70)]
    public required string Title { get; set; }
    public required MaterialType Type { get; set; }
    public required MaterialStatus Status { get; set; }
}