namespace Application.DTOs.Review;

public class QuestionReviewSessionDto
{
    public required Guid Id { get; init; }
    public required List<ReviewQuestionDto> Questions { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}

public class QuestionReviewSessionCreateDto
{
    public int? Limit { get; set; }
}
