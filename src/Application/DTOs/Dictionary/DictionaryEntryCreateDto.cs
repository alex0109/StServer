using Domain.Utility.Dictionary;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Dictionary;

public class DictionaryEntryCreateDto
{
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
    public List<string>? Synonyms { get; set; }
}