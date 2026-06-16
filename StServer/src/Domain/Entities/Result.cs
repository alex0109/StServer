using System.ComponentModel.DataAnnotations;
using StServer.Domain.Utility.Result;

namespace StServer.Domain.Entities;

public class Result
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Attempt Attempt { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    [MaxLength(1000)]
    public required string UserAnswer { get; set; }
    public required bool IsCorrect { get; set; }
    public TimeSpan TimeSpent { get; set; }
    public ConfidenceLevel? ConfidenceLevel { get; set; }
    public int AnswerChangedCount { get; set; } = 0;
    public DateTime AnsweredAt { get; set; }
}