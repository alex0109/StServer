using System.ComponentModel.DataAnnotations;
using Domain.Utility.Result;

namespace Application.DTOs.Result;

public class ResultRequestDto
{
    public Guid QuestionId { get; set; }
    [MaxLength(1000)]
    public string? UserAnswer { get; set; }
    public Guid? UserAnswerOptionId { get; set; }
    public ConfidenceLevel? ConfidenceLevel { get; set; }
}