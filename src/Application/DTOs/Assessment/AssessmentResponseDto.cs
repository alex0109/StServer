using Domain.Utility.Question;

namespace Application.DTOs.Assessment;

public class AssessmentResponseDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public int? TimerMinutes { get; set; }
    // The levels an attempt draws from, every level when none was chosen
    public List<QuestionDifficulty> Difficulties { get; set; } = [];
    public int? QuestionCount { get; set; }
}
