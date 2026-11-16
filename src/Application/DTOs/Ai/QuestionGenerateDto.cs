using Domain.Utility.Question;

namespace Application.DTOs.Ai;

public class QuestionGenerateDto
{
    // Questions come from this note, or from the material content when empty
    public Guid? NoteId { get; set; }
    // Questions come from the subtitles of the material's video; every one leads to its moment
    public bool FromVideo { get; set; }
    public int Count { get; set; } = 3;
    // Null mixes all types
    public QuestionType? QuestionType { get; set; }
}
