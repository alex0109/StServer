using Domain.Utility.Question;

namespace Application.DTOs.Question;

public class OpenQuestionCreateDto
{
    public Guid MaterialId { get; set; }
    public required string Title { get; set; }
    public required string Answer { get; set; }
    public string? Explanation { get; set; }
    public QuestionDifficulty QuestionDifficulty { get; set; }
}