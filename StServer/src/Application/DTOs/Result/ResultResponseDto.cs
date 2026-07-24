using StServer.Domain.Utility.Result;

namespace StServer.Application.DTOs.Result;

public class ResultResponseDto
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
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