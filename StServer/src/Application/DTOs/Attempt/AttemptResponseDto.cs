using StServer.Domain.Utility.Attempt;

namespace StServer.Application.DTOs.Attempt;

public class AttemptResponseDto
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public AttemptStatus AttemptStatus  { get; set; }
    public int Score { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}