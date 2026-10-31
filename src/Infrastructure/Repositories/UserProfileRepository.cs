using Application.Interfaces;
using Application.Notifications;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly AppDbContext _db;

    public UserProfileRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<UserProfile?> GetAsync(Guid userId) =>
        _db.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);

    public async Task AddAsync(UserProfile profile)
    {
        await _db.UserProfiles.AddAsync(profile);
    }

    public async Task<string?> GetTimeZoneAsync(Guid userId)
    {
        var fromProfile = await _db.UserProfiles.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.TimeZone)
            .FirstOrDefaultAsync();

        return fromProfile ?? await _db.UserNotificationSettings.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.TimeZone)
            .FirstOrDefaultAsync();
    }

    public Task<List<NotificationRecipient>> GetRecipientsPageAsync(int skip, int take) =>
        (from profile in _db.UserProfiles.AsNoTracking()
         where profile.TimeZone != null
         join settings in _db.UserNotificationSettings.AsNoTracking() on profile.UserId equals settings.UserId into joined
         from settings in joined.DefaultIfEmpty()
         orderby profile.UserId
         select new NotificationRecipient(
             profile.UserId,
             profile.TimeZone!,
             profile.InterfaceLanguage,
             settings == null ? null : settings.Email,
             settings != null && settings.EmailDailyReminder,
             settings != null && settings.EmailWeeklyDigest,
             settings != null && settings.EmailStreakReminder,
             profile.DailyReminderHour,
             profile.StreakReminderHour))
        .Skip(skip)
        .Take(take)
        .ToListAsync();

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
