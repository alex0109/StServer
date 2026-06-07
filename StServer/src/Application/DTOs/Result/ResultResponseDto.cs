namespace StServer.Application.DTOs.Result;

public class ResultResponseDto
{
    public Guid Id { get; set; }
    
    public Guid AssessmentId { get; set; }
    
    public Guid QuestionId { get; set; }
    
    public required string UserAnswer { get; set; }
    
    public required bool IsCorrect  { get; set; }
    
    public required DateTime AnsweredAt { get; set; }
}