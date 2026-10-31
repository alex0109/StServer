using Domain.Utility.Flashcard;

namespace Application.DTOs.Flashcard;

public class FlashcardSessionSummaryDto
{
    public Guid Id { get; set; }
    // Null for a session of the whole "Today" queue
    public Guid? DictionaryId { get; set; }
    public FlashcardSessionScope Scope { get; set; }
    public FlashcardSessionMode Mode { get; set; }
    public int CardsTotal { get; set; }
    public int Reviewed { get; set; }
    public int Again { get; set; }
    public int Hard { get; set; }
    public int Good { get; set; }
    public int Easy { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
