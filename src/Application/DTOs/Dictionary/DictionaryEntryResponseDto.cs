using Domain.Utility.Dictionary;

namespace Application.DTOs.Dictionary;

public class DictionaryEntryResponseDto
{
    public Guid Id { get; set; }
    public Guid DictionaryId { get; set; }
    public required string Term { get; set; }
    public required string Translation { get; set; }
    public string? Transcription { get; set; }
    public PartOfSpeech? PartOfSpeech { get; set; }
    public string? GrammarInfo { get; set; }
    public string? Example { get; set; }
    public string? ExampleTranslation { get; set; }
    public string? Note { get; set; }
    public List<string> Synonyms { get; set; } = [];
    public int Position { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}