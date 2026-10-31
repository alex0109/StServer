using Domain.Utility.Dictionary;

namespace Application.DTOs.Ai;

public class WordLookupRequestDto
{
    public string? Term { get; set; }
    // The sentence the word was selected in; it becomes the example
    public string? Sentence { get; set; }
    // Language of the translation; the profile's native language when empty
    public string? Language { get; set; }
}

// A suggestion only: nothing is saved until the user saves the word
public class WordLookupDto
{
    public required string Term { get; init; }
    public required string Translation { get; init; }
    public string? Transcription { get; init; }
    public PartOfSpeech? PartOfSpeech { get; init; }
    public string? GrammarInfo { get; init; }
    public string? Example { get; init; }
    public string? ExampleTranslation { get; init; }
    public required string Language { get; init; }
    // Set when the dictionary already has this term
    public Guid? ExistingEntryId { get; init; }
}
