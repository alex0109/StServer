using Application.DTOs.Option;
using Domain.Utility.Question;

namespace Application.DTOs.Question;

public class QuestionUpdateDto
{
    public string? Title { get; set; }
    public string? Answer { get; set; }
    public Guid? CorrectOptionId { get; set; }
    public required List<OptionUpdateDto>? Options { get; set; }
    public QuestionDifficulty? QuestionDifficulty { get; set; }
    public string? Explanation { get; set; }
}