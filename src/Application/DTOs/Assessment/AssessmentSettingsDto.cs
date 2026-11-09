using Domain.Utility.Question;

namespace Application.DTOs.Assessment;

public class AssessmentSettingsDto
{
    // Null turns the timer off
    public int? TimerMinutes { get; set; }
    // Levels the attempt draws from; null or empty takes all of them
    public List<QuestionDifficulty>? Difficulties { get; set; }
    // Null takes every question that matches
    public int? QuestionCount { get; set; }
}
