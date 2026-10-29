namespace Application.DTOs.Statistics;

public class StudyStreakDto
{
    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    // Days are counted in the user's time zone, a finished attempt or a flashcard review makes a day
    public bool StudiedToday { get; set; }
    // Last 14 days, oldest first, the last one is today
    public List<bool> RecentDays { get; set; } = [];
}
