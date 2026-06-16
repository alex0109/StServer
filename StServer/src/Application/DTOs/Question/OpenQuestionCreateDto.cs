using StServer.Domain.Utility.Question;

namespace StServer.Application.DTOs.Question;

public class OpenQuestionCreateDto
{
    public Guid MaterialId { get; set; }
    public required string Title { get; set; }
    public required string Answer { get; set; }
    public QuestionDifficulty QuestionDifficulty { get; set; }
}