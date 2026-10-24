using Application.DTOs.Note;

namespace Application.DTOs.Ai;

public class SummaryGenerateResponseDto
{
    public required NoteResponseDto Note { get; init; }
    // True when the material was too long and only its beginning was summarized
    public required bool IsContentTruncated { get; init; }
}
