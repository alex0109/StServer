using Application.Schedulers;

namespace Application.Profile;

// A user's learning settings with the global defaults filled in
public record LearningSettings(
    double? DesiredRetention,
    int? NewCardsPerDay,
    int? MaxReviewsPerDay,
    int DailyReminderHour,
    int StreakReminderHour,
    int? DailyGoal,
    int StreakFreezesPerMonth)
{
    public SchedulerOverrides? Overrides => DesiredRetention is null ? null : new SchedulerOverrides(DesiredRetention);
}

// What is done and what is left of the daily limits in the user's local day; null is "no limit"
public record DailyBudget(int ReviewsDone, int NewWordsDone, int? NewWordsLeft, int? ReviewsLeft)
{
    public static readonly DailyBudget Unlimited = new(0, 0, null, null);
}

// Bounds of the settings; the client shows the same ones
public static class LearningRules
{
    public const double MinRetention = 0.80;
    public const double MaxRetention = 0.97;
    public const int MaxNewCardsPerDay = 1000;
    public const int MaxReviewsPerDay = 5000;
    public const int MaxStreakFreezes = 10;
    public const int DefaultStreakFreezesPerMonth = 2;
}
