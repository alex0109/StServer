namespace Application.DTOs.Statistics;

public class TagPerformanceDto
{
    public Guid TagId { get; set; }
    public required string TagName { get; set; }
    public int AnswersCount { get; set; }
    public double Accuracy { get; set; }
}
