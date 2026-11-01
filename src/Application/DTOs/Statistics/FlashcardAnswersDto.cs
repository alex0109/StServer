namespace Application.DTOs.Statistics;

// How the user answered flashcards over the last days: the three buttons of a card
public class FlashcardAnswersDto
{
    public int Know { get; set; }
    public int Unsure { get; set; }
    public int DontKnow { get; set; }

    // Every day of the window in the user's own calendar, oldest first, days without answers included
    public List<FlashcardAnswersDayDto> Days { get; set; } = [];

    // Words answered "don't know" most often in the window, the most often first
    public List<HardWordDto> HardestWords { get; set; } = [];
}

public class FlashcardAnswersDayDto
{
    public DateOnly Date { get; set; }
    public int Know { get; set; }
    public int Unsure { get; set; }
    public int DontKnow { get; set; }
}

public class HardWordDto
{
    public Guid EntryId { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public int DontKnow { get; set; }
    public int Answers { get; set; }
}
