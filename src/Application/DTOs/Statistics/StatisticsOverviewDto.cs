namespace Application.DTOs.Statistics;

public class StatisticsOverviewDto
{
    public int TotalMaterials { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalFinishedAttempts { get; set; }

    // correctAnswers / totalAnswers * 100
    public double OverallAccuracy { get; set; }

    // average of (average Result.Score per Attempt) across all attempts
    public double AverageScorePerAttempt { get; set; }

    // standard deviation of per-attempt average scores — lower = more consistent performance
    public double ConsistencyScore { get; set; }

    public TimeSpan TotalTimeSpent { get; set; }
    public TimeSpan AverageTimePerQuestion { get; set; }
}
