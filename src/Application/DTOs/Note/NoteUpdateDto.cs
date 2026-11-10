using System.Text.Json;

namespace Application.DTOs.Note;

public class NoteUpdateDto
{
    public string? Title { get; set; }
    public RichTextDocument.RichTextDocument? TextContent { get; set; }
    public JsonDocument? DrawingContent { get; set; }
    // Essays only; an empty string removes the task
    public string? EssayTask { get; set; }
    public int? Order { get; set; }
}
