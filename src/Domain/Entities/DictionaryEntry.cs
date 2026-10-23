using System.ComponentModel.DataAnnotations;
using Domain.Utility.Dictionary;

namespace Domain.Entities;

public class DictionaryEntry
{
    public Guid Id { get; set; }
    public Guid DictionaryId { get; set; }
    public Dictionary Dictionary { get; set; } = null!;
    [MaxLength(200)]
    public required string Term { get; set; }
    [MaxLength(200)]
    public required string Translation { get; set; }
    [MaxLength(200)]
    public string? Transcription { get; set; }
    public PartOfSpeech? PartOfSpeech { get; set; }
    [MaxLength(300)]
    public string? GrammarInfo { get; set; }
    [MaxLength(500)]
    public string? Example { get; set; }
    [MaxLength(500)]
    public string? ExampleTranslation { get; set; }
    [MaxLength(1000)]
    public string? Note { get; set; }
    public List<string> Synonyms { get; set; } = [];
    public int Position { get; set; }
    public bool IsArchived { get; set; }
    public Question? Question { get; set; }
    public DictionaryEntryProgress? Progress { get; set; }
    public ICollection<FlashcardReview> Reviews { get; set; } = new List<FlashcardReview>();
    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}