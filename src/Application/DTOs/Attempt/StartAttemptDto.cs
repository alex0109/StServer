namespace Application.DTOs.Attempt;

public class StartAttemptDto
{
    public Guid AssessmentId { get; set; }
    // The material's exam settings apply only to an attempt started from the material, not from a note
    public bool UseTimer { get; set; }
}