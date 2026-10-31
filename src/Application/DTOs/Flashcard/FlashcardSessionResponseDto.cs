using Application.DTOs.Dictionary;
using Domain.Utility.Flashcard;

namespace Application.DTOs.Flashcard;

public class FlashcardSessionResponseDto
{
    public Guid Id { get; set; }
    // Null for a session of the whole "Today" queue
    public Guid? DictionaryId { get; set; }
    public FlashcardSessionScope Scope { get; set; }
    public FlashcardSessionMode Mode { get; set; }
    public int CardsTotal { get; set; }
    public List<DictionaryEntryResponseDto> Cards { get; set; } = [];
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
