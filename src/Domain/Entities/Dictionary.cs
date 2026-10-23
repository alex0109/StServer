using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace Domain.Entities;

public class Dictionary
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    [MaxLength(70)]
    public required string Title { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    public ICollection<DictionaryEntry> Entries { get; set; } = new List<DictionaryEntry>();
    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}