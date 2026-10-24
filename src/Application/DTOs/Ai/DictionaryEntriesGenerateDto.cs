namespace Application.DTOs.Ai;

public class DictionaryEntriesGenerateDto
{
    // Words come from this note of the dictionary's material, or from the material content when empty
    public Guid? NoteId { get; set; }
    public int Count { get; set; } = 10;
    // Name of the translation language, e.g. "Ukrainian"
    public required string Language { get; set; }
}
