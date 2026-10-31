namespace Application.DTOs.Review;

public class MaterialDueDto
{
    public required Guid MaterialId { get; init; }
    public required string Title { get; init; }
    // Questions whose next review is due by the end of the day
    public required int Due { get; init; }
}
