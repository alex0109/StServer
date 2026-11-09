using Domain.Utility.Attempt;

namespace Domain.Entities;

public class Attempt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ICollection<Result> Results { get; set; } = new List<Result>();
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = null!;
    public AttemptStatus AttemptStatus  { get; set; }
    public required DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    // The limit at the start: changing the settings later doesn't touch a running attempt
    public int? TimeLimitMinutes { get; set; }
    // The questions drawn at the start, in the order they are shown. Empty for attempts from before
    // the draw existed: those have every active question of the material
    public List<Guid> QuestionIds { get; set; } = [];
}
