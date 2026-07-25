using System.ComponentModel.DataAnnotations;

namespace StServer.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(50)]
    public required string Name { get; set; } = null!;
    public required string Color { get; set; } = null!;
    public ICollection<MaterialTag> MaterialTags { get; set; } = new List<MaterialTag>();
}