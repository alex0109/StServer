namespace Application.DTOs.Ai;

// How a dictionary word is used in everyday life. Shown once, never saved
public class WordExplanationDto
{
    public required string Term { get; init; }
    public required string Meaning { get; init; }
    public required List<WordUsageExampleDto> Examples { get; init; }
    public required List<string> Collocations { get; init; }
    public string? Tip { get; init; }
}

public class WordUsageExampleDto
{
    // In the language of the word
    public required string Sentence { get; init; }
    public required string Translation { get; init; }
    // Where you would say it: "at a café", "at work"
    public required string Situation { get; init; }
}
