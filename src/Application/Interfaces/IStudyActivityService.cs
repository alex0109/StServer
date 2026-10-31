using Application.DTOs.Notification;
using Application.DTOs.Statistics;

namespace Application.Interfaces;

// Study activity in the user's own calendar, shared by statistics, the feed and reminders
public interface IStudyActivityService
{
    Task<StudyStreakDto> GetStreakAsync(Guid userId, TimeZoneInfo timeZone, DateTime utcNow);
    Task<TodayDto> GetTodayAsync(Guid userId, TimeZoneInfo timeZone, DateTime utcNow);
    // The seven local days before weekEnd
    Task<WeeklyDigestData> GetWeekAsync(Guid userId, TimeZoneInfo timeZone, DateOnly weekEnd, DateTime utcNow);
}
