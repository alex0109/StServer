namespace Application.DTOs.Result;

public class ResultRequestDto
{
    public Guid QuestionId { get; set; }
    public string? UserAnswer { get; set; }
    public Guid? UserAnswerOptionId { get; set; }
}