namespace StServer.Application.DTOs.Assessment;

public class AssessmentUpdateDto
{
    public int? TotalQuestions { get; set; }

    public int? CorrectAnswers { get; set; }

    public int? Score { get; set; }
}