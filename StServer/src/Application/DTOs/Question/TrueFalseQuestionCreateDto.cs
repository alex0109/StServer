using StServer.Application.DTOs.Option;
using StServer.Domain.Utility.Question;

namespace StServer.Application.DTOs.Question;

public class TrueFalseQuestionCreateDto
{
    public Guid MaterialId { get; set; }
    public required string Title { get; set; }
    public Guid CorrectOptionId { get; set; }
    public QuestionDifficulty QuestionDifficulty { get; set; }
    public List<OptionDto> Options { get; set; } = new();
}