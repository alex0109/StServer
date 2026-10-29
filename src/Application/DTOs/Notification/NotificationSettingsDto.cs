namespace Application.DTOs.Notification;

public class NotificationSettingsDto
{
    // Null until the client sent the time zone for the first time
    public string? TimeZone { get; init; }
    public required string Language { get; init; }
    // Address the emails go to, null while emails are off
    public string? Email { get; init; }
    public bool EmailDailyReminder { get; init; }
    public bool EmailWeeklyDigest { get; init; }
    public bool EmailStreakReminder { get; init; }
}

// Only the sent fields change
public class NotificationSettingsUpdateDto
{
    public string? TimeZone { get; set; }
    public string? Language { get; set; }
    public bool? EmailDailyReminder { get; set; }
    public bool? EmailWeeklyDigest { get; set; }
    public bool? EmailStreakReminder { get; set; }
}
