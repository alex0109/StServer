using StServer.Application.DTOs.Option;
using StServer.Domain.Utility.Question;

namespace StServer.Application.DTOs.Question;

public class OptionQuestionCreateDto
{
    public Guid MaterialId { get; set; }
    public required string Title { get; set; }
    public required Guid CorrectOptionId { get; set; }
    public QuestionDifficulty QuestionDifficulty { get; set; }
    public required List<OptionUpdateDto> Options { get; set; } = [];
    public string? Explanation { get; set; }
}