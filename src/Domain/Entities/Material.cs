using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Domain.Utility.Material;

namespace Domain.Entities;

public class Material
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public ICollection<MaterialFile> MaterialFiles { get; set; } = new List<MaterialFile>();
    public ICollection<MaterialTag> MaterialTags { get; set; } = new List<MaterialTag>();
    [MaxLength(70)]
    public required string Title { get; set; }
    public required MaterialType Type { get; set; }
    public string? Link { get; set; }
    public JsonDocument? Content { get; set; }
    public required MaterialStatus Status { get; set; }
    public required bool IsActive { get; set; } = true;
    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int Version { get; set; } = 1;
    
    public void AddTag(Guid tagId)
    {
        if (MaterialTags.Any(x => x.TagId == tagId))
            return;

        MaterialTags.Add(new MaterialTag
        {
            MaterialId = Id,
            TagId = tagId
        });
    }
    
    public void RemoveTag(Guid tagId)
    {
        var materialTag = MaterialTags
            .FirstOrDefault(x => x.TagId == tagId);

        if (materialTag is null)
            return;

        MaterialTags.Remove(materialTag);
    }
}