using StServer.Domain.Utility.Attempt;

namespace StServer.Domain.Entities;

public class Attempt
{
    public Guid Id { get; set; }
    public ICollection<Result> Results { get; set; } = new List<Result>();
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = null!;
    public AttemptStatus AttemptStatus  { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}