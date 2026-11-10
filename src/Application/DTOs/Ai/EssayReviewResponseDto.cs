using Application.DTOs.Note;

namespace Application.DTOs.Ai;

public class EssayReviewResponseDto
{
    // The note with its new review
    public required NoteResponseDto Note { get; init; }
}
