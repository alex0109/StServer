using Domain.Utility.Flashcard;

namespace Domain.Entities;

public class FlashcardSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    // Null for sessions of the whole "Today" queue
    public Guid? DictionaryId { get; set; }
    public Dictionary? Dictionary { get; set; }
    public FlashcardSessionScope Scope { get; set; } = FlashcardSessionScope.Dictionary;
    public FlashcardSessionMode Mode { get; set; }
    public int CardsTotal { get; set; }
    public ICollection<FlashcardReview> Reviews { get; set; } = new List<FlashcardReview>();
    public required DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
