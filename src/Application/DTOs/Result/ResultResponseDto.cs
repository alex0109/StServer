using System.ComponentModel.DataAnnotations;
using Domain.Utility.Result;
using Domain.Utility.Review;

namespace Application.DTOs.Result;

public class ResultResponseDto
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    [MaxLength(1000)]
    public string? UserAnswer { get; set; }
    public List<Guid> UserAnswerOptionIds { get; set; } = [];
    public required bool IsCorrect  { get; set; }
    public required double Score { get; set; }
    public required int Weight { get; set; }
    public ConfidenceLevel? ConfidenceLevel { get; set; }
    public ReviewRating Rating { get; set; }
    // How the answer was checked, AI marks answers accepted by meaning
    public EvaluationMethod Method { get; set; }
    public string? AiExplanation { get; set; }
    // Filled for option and true/false questions, so results can show the right answers
    public List<Guid> CorrectOptionIds { get; set; } = [];
    // Model answer of an open question
    public string? CorrectAnswer { get; set; }
    // Why the right answer is right, written with the question
    public string? QuestionExplanation { get; set; }
    public int? AnswerChangedCount { get; set; }
    public TimeSpan TimeSpent { get; set; }
    public required DateTime AnsweredAt { get; set; }
}