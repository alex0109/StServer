namespace Application.DTOs.Flashcard;

public class FlashcardStatsResponseDto
{
    public int Total { get; set; }
    public int New { get; set; }
    public int Learning { get; set; }
    public int Review { get; set; }
    public int Relearning { get; set; }
    // Already reviewed cards whose due date has come, new cards are not included
    public int Due { get; set; }
}
