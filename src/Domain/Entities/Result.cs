using System.ComponentModel.DataAnnotations;
using Domain.Utility.Result;
using Domain.Utility.Review;

namespace Domain.Entities;

public class Result
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AttemptId { get; set; }
    public Attempt Attempt { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    [MaxLength(1000)]
    public string? UserAnswer { get; set; }
    public List<Guid> UserAnswerOptionIds { get; set; } = [];
    public required bool IsCorrect { get; set; }
    public required double Score { get; set; }
    public required int Weight { get; set; }
    public TimeSpan TimeSpent { get; set; }
    public ConfidenceLevel? ConfidenceLevel { get; set; }
    // FSRS rating of the answer, derived from correctness and confidence
    public ReviewRating Rating { get; set; }
    public EvaluationMethod Method { get; set; }
    // Cached AI explanation of a wrong answer, so it is paid for once
    [MaxLength(2000)]
    public string? AiExplanation { get; set; }
    // Explanation language the cached text was written for, null for texts from before the setting
    [MaxLength(16)]
    public string? AiExplanationLanguage { get; set; }
    public int AnswerChangedCount { get; set; } = 0;
    // Set when the user accepted an answer the automatic check rejected; the original grade is kept for undo
    public DateTime? OverriddenAt { get; set; }
    public bool? OriginalIsCorrect { get; set; }
    public double? OriginalScore { get; set; }
    public EvaluationMethod? OriginalMethod { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public required DateTime AnsweredAt { get; set; }
}