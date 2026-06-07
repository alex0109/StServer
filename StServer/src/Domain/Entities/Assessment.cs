namespace StServer.Domain.Entities;

public class Assessment
{
    public Guid Id { get; set; }
    public ICollection<Result> Results { get; set; } = new List<Result>();
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public int? TotalQuestions { get; set; }
    public int? CorrectAnswers { get; set; }
    public int? Score { get; set; }
    public required DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}