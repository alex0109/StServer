using StServer.Domain.Utility.Question;

namespace StServer.Application.DTOs.Question;

public class QuestionUpdateDto
{
    public string? Title { get; set; }
    public string? Answer { get; set; }
    public Guid? CorrectOptionId { get; set; }
    public QuestionDifficulty? QuestionDifficulty { get; set; }
    public string? Explanation { get; set; }
}