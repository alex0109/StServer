namespace Application.DTOs.Statistics;

public class StudyStreakDto
{
    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    // Days are counted in the user's time zone, a finished attempt or a flashcard review makes a day
    public bool StudiedToday { get; set; }
    // Last 14 days, oldest first, the last one is today
    public List<bool> RecentDays { get; set; } = [];
    // Same days: missed but covered by a streak freeze
    public List<bool> RecentFrozenDays { get; set; } = [];
    public int FreezesLeft { get; set; }
    public int FreezesPerMonth { get; set; }
    // With a goal a day counts once this many reviews are done; null means any study counts
    public int? DailyGoal { get; set; }
    public int ReviewsToday { get; set; }
}
