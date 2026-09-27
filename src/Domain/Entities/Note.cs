using System.Text.Json;
using Domain.Utility.Note;

namespace Domain.Entities;

public class Note
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public required string Title { get; set; }
    public NoteType Type { get; set; }
    
    public JsonDocument? TextContent { get; set; }
    
    public JsonDocument? DrawingContent { get; set; }
    
    public Guid? FileId { get; set; }
    public MaterialFile? File { get; set; }

    public int Order { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}