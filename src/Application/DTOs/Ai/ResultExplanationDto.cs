namespace Application.DTOs.Ai;

public class ResultExplanationDto
{
    public required Guid ResultId { get; init; }
    public required Guid QuestionId { get; init; }
    public required string Explanation { get; init; }
}
