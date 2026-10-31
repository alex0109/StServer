using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

// Settings of a person that don't belong to one feature: where they live, which languages they use,
// how they appear in the library. Made on the first visit, when the browser sends its time zone
public class UserProfile
{
    public Guid UserId { get; set; }

    // IANA name, e.g. "Europe/Oslo". Day boundaries, streaks and reminder hours follow it
    [MaxLength(64)]
    public string? TimeZone { get; set; }

    // Site and email language: en-GB, nb or uk
    [MaxLength(8)]
    public required string InterfaceLanguage { get; set; }

    // Language of AI explanations: a code from the list in ProfileLanguages,
    // "question" for the language of the question, null for the interface language
    [MaxLength(16)]
    public string? ExplanationLanguage { get; set; }

    // Plain language names, e.g. "Ukrainian" and "Norwegian"; they go into AI prompts
    [MaxLength(40)]
    public string? NativeLanguage { get; set; }
    [MaxLength(40)]
    public string? StudyLanguage { get; set; }

    // Shown on materials published in the library
    [MaxLength(40)]
    public string? Nickname { get; set; }

    // Reviews a day the user aims for. With a goal a streak day needs the goal met, without one any study counts
    public int? DailyGoal { get; set; }

    // Learning settings for people who want control; null everywhere means the global defaults.
    // Share of cards the user wants to still remember when they come back (FSRS), 0.80 to 0.97
    public double? DesiredRetention { get; set; }
    // Daily limits of the "Today" queue and "due" sessions, in the user's local day
    public int? NewCardsPerDay { get; set; }
    public int? MaxReviewsPerDay { get; set; }
    // Local hours of the reminders, 0 to 23
    public int? DailyReminderHour { get; set; }
    public int? StreakReminderHour { get; set; }
    // Missed days a month that don't break the streak
    public int? StreakFreezesPerMonth { get; set; }

    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
