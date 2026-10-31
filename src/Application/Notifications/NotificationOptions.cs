namespace Application.Notifications;

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    // Local hours of the user. A reminder is made on the first hourly run inside its window,
    // so one missed run of the job doesn't lose it
    public int DailyReminderHour { get; set; } = 9;
    public int StreakReminderHour { get; set; } = 20;
    public DayOfWeek WeeklyDigestDay { get; set; } = DayOfWeek.Monday;
    public int WeeklyDigestHour { get; set; } = 9;
    public int WindowHours { get; set; } = 3;

    // Old reminders are removed by the cleanup job
    public int KeepDays { get; set; } = 90;

    public static readonly string[] Languages = ["en-GB", "nb", "uk"];
    public const string DefaultLanguage = "en-GB";
}
