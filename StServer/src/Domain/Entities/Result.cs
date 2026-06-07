using System.ComponentModel.DataAnnotations;

namespace StServer.Domain.Entities;

public class Result
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    [MaxLength(1000)]
    public required string UserAnswer { get; set; }
    public required bool IsCorrect  { get; set; }
    public required DateTime AnsweredAt { get; set; }
}