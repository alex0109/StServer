namespace Application.DTOs.Dictionary;

public class DictionaryEntriesReorderDto
{
    public required List<Guid> EntryIds { get; set; }
}