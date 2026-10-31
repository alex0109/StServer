using Domain.Utility.Question;

namespace Application.DTOs.Question;

public class TrueFalseQuestionCreateDto
{
    public Guid MaterialId { get; set; }
    public Guid? NoteId { get; set; }
    public required string Title { get; set; }
    public required bool IsTrue { get; set; }
    public QuestionDifficulty QuestionDifficulty { get; set; }
    public string? Explanation { get; set; }
}
