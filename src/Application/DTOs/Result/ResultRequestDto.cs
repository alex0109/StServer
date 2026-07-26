using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Result;

public class ResultRequestDto
{
    public Guid QuestionId { get; set; }
    [MaxLength(1000)]
    public string? UserAnswer { get; set; }
    public Guid? UserAnswerOptionId { get; set; }
}