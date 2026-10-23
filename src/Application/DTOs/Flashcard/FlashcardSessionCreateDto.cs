using Domain.Utility.Flashcard;

namespace Application.DTOs.Flashcard;

public class FlashcardSessionCreateDto
{
    public FlashcardSessionMode Mode { get; set; }
    public int? Limit { get; set; }
    // Required for Custom mode
    public List<Guid>? EntryIds { get; set; }
}
