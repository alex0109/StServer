namespace Application.Interfaces;

// The one place a user's time zone is read from: day boundaries for reviews, streaks and reminders
public interface IUserTimeZoneProvider
{
    Task<TimeZoneInfo> GetAsync(Guid userId);
}
