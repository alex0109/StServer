using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

// Where and how a user gets reminders. In-app reminders need only the time zone,
// emails are sent only for the kinds the user turned on
public class UserNotificationSettings
{
    public Guid UserId { get; set; }
    // IANA name from the browser, e.g. "Europe/Kyiv"
    [MaxLength(64)]
    public required string TimeZone { get; set; }
    // Language of the emails: en-GB, nb or uk
    [MaxLength(8)]
    public required string Language { get; set; }
    // Saved when the user turns emails on, from the signed in account
    [MaxLength(254)]
    public string? Email { get; set; }
    public bool EmailDailyReminder { get; set; }
    public bool EmailWeeklyDigest { get; set; }
    public bool EmailStreakReminder { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
