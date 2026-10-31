namespace Application.Notifications;

// Who the reminder job writes to: profile values plus the email choices
public record NotificationRecipient(
    Guid UserId,
    string TimeZone,
    string Language,
    string? Email,
    bool EmailDailyReminder,
    bool EmailWeeklyDigest,
    bool EmailStreakReminder,
    // The user's own local hours, null for the global ones
    int? DailyReminderHour = null,
    int? StreakReminderHour = null);
