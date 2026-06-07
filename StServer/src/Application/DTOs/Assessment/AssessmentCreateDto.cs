namespace StServer.Application.DTOs.Assessment;

public class AssessmentCreateDto
{
    public Guid MaterialId { get; set; }

    public int? TotalQuestion { get; set; }

    public int? CorrectAnswers { get; set; }

    public int? Score { get; set; }
}