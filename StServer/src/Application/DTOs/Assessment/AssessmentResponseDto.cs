namespace StServer.Application.DTOs.Assessment;

public class AssessmentResponseDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public int TotalAttempts { get; set; }
    public int AverageScore { get; set; }
    public int BestScore { get; set; }
    public DateTime? LastAttemptAt { get; set; }
}