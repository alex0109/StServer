using Application.DTOs.Statistics;

namespace Application.DTOs.Notification;

public record DailyReviewData(int DueWords, int NewWords, List<DictionaryDueDto> Dictionaries);

public record StreakAtRiskData(int StreakDays);

public record WeakTopicData(string Name, double Accuracy, int Answers);

public record WeeklyDigestData(
    string From,
    string To,
    int StudyDays,
    int StreakDays,
    int Attempts,
    int Answers,
    double? Accuracy,
    double? PreviousAccuracy,
    double? AverageScore,
    int Reviews,
    List<WeakTopicData> WeakestTopics)
{
    public bool HasActivity => StudyDays > 0 || Attempts > 0 || Reviews > 0;
}
