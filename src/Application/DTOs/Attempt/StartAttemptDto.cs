namespace Application.DTOs.Attempt;

public class StartAttemptDto
{
    public Guid AssessmentId { get; set; }
    // The material's exam settings (levels, number of questions, timer); without them the attempt
    // has every active question and no timer
    public bool UseExamSettings { get; set; }
}
