namespace Application.DTOs.Profile;

public class ProfileDto
{
    // Null until the browser sent it for the first time
    public string? TimeZone { get; init; }
    public required string InterfaceLanguage { get; init; }
    // The saved setting: a code, "question", or null for the interface language
    public string? ExplanationLanguage { get; init; }
    // What explanations are written in right now
    public required string EffectiveExplanationLanguage { get; init; }
    public string? NativeLanguage { get; init; }
    public string? StudyLanguage { get; init; }
    public string? Nickname { get; init; }
    public int? DailyGoal { get; init; }

    // Learning settings; null means the default shown in Defaults
    public double? DesiredRetention { get; init; }
    public int? NewCardsPerDay { get; init; }
    public int? MaxReviewsPerDay { get; init; }
    public int? DailyReminderHour { get; init; }
    public int? StreakReminderHour { get; init; }
    public int? StreakFreezesPerMonth { get; init; }
    public required LearningDefaultsDto Defaults { get; init; }
}

public record LearningDefaultsDto(
    double DesiredRetention,
    int DailyReminderHour,
    int StreakReminderHour,
    int StreakFreezesPerMonth,
    double MinRetention,
    double MaxRetention);

// Only the sent fields change. An empty string clears a text field, 0 clears the daily goal
public class ProfileUpdateDto
{
    public string? TimeZone { get; set; }
    public string? InterfaceLanguage { get; set; }
    public string? ExplanationLanguage { get; set; }
    public string? NativeLanguage { get; set; }
    public string? StudyLanguage { get; set; }
    public string? Nickname { get; set; }
    public int? DailyGoal { get; set; }

    // 0 clears the retention; -1 clears the others (0 new cards a day is a real choice)
    public double? DesiredRetention { get; set; }
    public int? NewCardsPerDay { get; set; }
    public int? MaxReviewsPerDay { get; set; }
    public int? DailyReminderHour { get; set; }
    public int? StreakReminderHour { get; set; }
    public int? StreakFreezesPerMonth { get; set; }
}
