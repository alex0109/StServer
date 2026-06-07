namespace StServer.Application.DTOs.Result;

public class ResultCreateDto
{
    public Guid AssessmentId { get; set; }
    
    public Guid QuestionId { get; set; }
    
    public required string UserAnswer { get; set; }
    
    public required bool IsCorrect  { get; set; }
}