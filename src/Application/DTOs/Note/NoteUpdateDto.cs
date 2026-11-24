using System.Text.Json;

namespace Application.DTOs.Note;

public class NoteUpdateDto
{
    public string? Title { get; set; }
    public RichTextDocument.RichTextDocument? TextContent { get; set; }
    public JsonDocument? DrawingContent { get; set; }
    // Essays only; an empty string removes the task
    public string? EssayTask { get; set; }
    // Essays only: the exam it is written for, like "Ielts/B2/Letter"; an empty string makes it a general essay
    public string? EssayFormat { get; set; }
    public int? Order { get; set; }
}
