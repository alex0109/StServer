using StServer.Domain.Utility.Material;

namespace StServer.Application.DTOs.Material;

public class MaterialCreateDto
{
    public required string Title { get; set; }
    public required MaterialType Type { get; set; }
    public List<string> MaterialTags { get; set; } = new List<string>();
    public required MaterialStatus Status { get; set; }
}