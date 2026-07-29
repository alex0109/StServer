using Domain.Utility.Question;

namespace Application.DTOs.Statistics;

public class DifficultyStatDto
{
    public QuestionDifficulty Difficulty { get; set; }
    public int AnswersCount { get; set; }
    public double Accuracy { get; set; }
    public TimeSpan AverageTimeSpent { get; set; }
}
