namespace StServer.Application.DTOs.Assessment;

public class AssessmentResponseDto
{
    public Guid Id { get; set; }
    
    public Guid MaterialId { get; set; }

    public int? TotalQuestions { get; set; }

    public int? CorrectAnswers { get; set; }

    public int? Score { get; set; }

    public required DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }
}