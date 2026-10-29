namespace Application.DTOs.Statistics;

public class DictionaryDueDto
{
    public required Guid DictionaryId { get; init; }
    public required Guid MaterialId { get; init; }
    public required string Title { get; init; }
    // Reviewed words whose next review is due by the end of the day
    public required int Due { get; init; }
    // Words never reviewed yet
    public required int New { get; init; }
}
