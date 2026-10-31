using Domain.Utility.Flashcard;
using Domain.Utility.Result;
using Domain.Utility.Review;

namespace Application.DTOs.Review;

// Feedback right after an answer in review, with what was right and when the question returns
public class ReviewAnswerResultDto
{
    public required Guid QuestionId { get; init; }
    public required bool IsCorrect { get; init; }
    public required double Score { get; init; }
    public required EvaluationMethod Method { get; init; }
    public required ReviewRating Rating { get; init; }
    public string? CorrectAnswer { get; init; }
    public List<Guid> CorrectOptionIds { get; init; } = [];
    public string? Explanation { get; init; }
    public required FlashcardState State { get; init; }
    public required DateTime NextDue { get; init; }
}
