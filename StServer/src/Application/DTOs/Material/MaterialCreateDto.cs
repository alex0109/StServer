using StServer.Application.DTOs.Tag;
using StServer.Domain.Utility.Material;

namespace StServer.Application.DTOs.Material;

public class MaterialCreateDto
{
    public required string Title { get; set; }
    public required MaterialType Type { get; set; }
    public required MaterialStatus Status { get; set; }
}