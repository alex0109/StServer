using System.Text.Json;

namespace StServer.Entities;

public class Material
{
    public Guid Id { get; set; }
    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
    public required string Title { get; set; }
    public required string Type { get; set; }
    public string[]? Tags { get; set; }
    public string? Link { get; set; }
    public JsonDocument? Description { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}