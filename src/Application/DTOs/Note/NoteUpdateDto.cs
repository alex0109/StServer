using System.Text.Json;
using Domain.Entities;

namespace Application.DTOs.Note;

public class NoteUpdateDto
{
    public string? Title { get; set; }
    public string? TextContent { get; set; }
    public JsonDocument? DrawingContent { get; set; }
    public int? Order { get; set; }
}