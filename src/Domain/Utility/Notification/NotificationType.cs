namespace Domain.Utility.Notification;

public enum NotificationType
{
    // Morning: words waiting for review today
    DailyReview = 1,
    // Evening: the streak ends unless the user studies today
    StreakAtRisk = 2,
    // Monday: summary of the previous week
    WeeklyDigest = 3
}
