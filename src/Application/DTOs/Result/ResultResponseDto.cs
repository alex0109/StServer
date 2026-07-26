using System.ComponentModel.DataAnnotations;
using Domain.Utility.Result;

namespace Application.DTOs.Result;

public class ResultResponseDto
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    [MaxLength(1000)]
    public string? UserAnswer { get; set; }
    public Guid? UserAnswerOptionId { get; set; }
    public required bool IsCorrect  { get; set; }
    public required double Score { get; set; }
    public required int Weight { get; set; }
    public ConfidenceLevel? ConfidenceLevel { get; set; }
    public int? AnswerChangedCount { get; set; }
    public TimeSpan TimeSpent { get; set; }
    public required DateTime AnsweredAt { get; set; }
}