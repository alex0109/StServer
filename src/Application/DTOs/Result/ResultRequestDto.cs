using System.ComponentModel.DataAnnotations;
using Domain.Utility.Result;

namespace Application.DTOs.Result;

public class ResultRequestDto
{
    public Guid QuestionId { get; set; }
    [MaxLength(1000)]
    public string? UserAnswer { get; set; }
    public List<Guid>? UserAnswerOptionIds { get; set; }
    public ConfidenceLevel? ConfidenceLevel { get; set; }
    // How many times the choice was changed before this answer was saved
    [Range(0, 1000)]
    public int? AnswerChanges { get; set; }
}