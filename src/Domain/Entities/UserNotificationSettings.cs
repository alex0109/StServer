using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

// Which reminder emails a user gets and where to. The time zone and language live in UserProfile
public class UserNotificationSettings
{
    public Guid UserId { get; set; }
    // Legacy copies: UserProfile is the source now. Still written, so a server of the previous
    // version reads the same values; to be dropped in a later migration
    [MaxLength(64)]
    public required string TimeZone { get; set; }
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
