using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

// Public link to a material, anyone with the token can import a copy of it
public class MaterialShare
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public Guid UserId { get; set; }
    [MaxLength(64)]
    public required string Token { get; set; }
    public int ImportsCount { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
