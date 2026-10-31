using Application.DTOs.Statistics;

namespace Application.DTOs.Notification;

// DueQuestions defaults to 0 for reminders saved before questions had repetition
public record DailyReviewData(int DueWords, int NewWords, List<DictionaryDueDto> Dictionaries, int DueQuestions = 0);

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
