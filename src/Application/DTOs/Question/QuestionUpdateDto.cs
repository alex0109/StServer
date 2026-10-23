using Application.DTOs.Option;
using Domain.Utility.Question;

namespace Application.DTOs.Question;

public class QuestionUpdateDto
{
    public string? Title { get; set; }
    public string? Answer { get; set; }
    // Makes this option the only correct one, used by True/False questions
    public Guid? CorrectOptionId { get; set; }
    public List<OptionUpdateDto>? Options { get; set; }
    public QuestionDifficulty? QuestionDifficulty { get; set; }
    public string? Explanation { get; set; }
}