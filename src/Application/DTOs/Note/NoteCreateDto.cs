using Domain.Utility.Note;

namespace Application.DTOs.Note;

public class NoteCreateDto
{
    public required string Title { get; set; }
    public required NoteType Type { get; set; }
}