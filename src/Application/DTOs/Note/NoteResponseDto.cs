using System.Text.Json;
using Domain.Utility.Note;

namespace Application.DTOs.Note;

public class NoteResponseDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public required string Title { get; set; }
    public NoteType Type { get; set; }
    public RichTextDocument.RichTextDocument? TextContent { get; set; }
    public JsonDocument? DrawingContent { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
