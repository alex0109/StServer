using Domain.Utility.Question;

namespace Application.DTOs.Ai;

public class QuestionGenerateDto
{
    // Questions come from this note, or from the material content when empty
    public Guid? NoteId { get; set; }
    public int Count { get; set; } = 3;
    // Null mixes all types
    public QuestionType? QuestionType { get; set; }
}
