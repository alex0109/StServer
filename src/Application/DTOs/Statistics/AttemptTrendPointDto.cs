namespace Application.DTOs.Statistics;

public class AttemptTrendPointDto
{
    public DateTime Date { get; set; }
    public int AttemptsCount { get; set; }
    public double AverageScore { get; set; }
}
