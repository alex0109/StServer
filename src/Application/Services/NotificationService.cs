using System.Text.Json;
using Application.DTOs.Notification;
using Application.DTOs.Profile;
using Application.Exceptions;
using Application.Interfaces;
using Application.Notifications;
using Application.Statistics;
using Domain.Entities;

namespace Application.Services;

public class NotificationService : INotificationService
{
    private const int FeedSize = 10;

    private readonly INotificationRepository _repo;
    private readonly IStudyActivityService _activity;
    private readonly IUserProfileService _profiles;
    private readonly IUserTimeZoneProvider _timeZones;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public NotificationService(
        INotificationRepository repo,
        IStudyActivityService activity,
        IUserProfileService profiles,
        IUserTimeZoneProvider timeZones,
        IUserContext user,
        TimeProvider time)
    {
        _repo = repo;
        _activity = activity;
        _profiles = profiles;
        _timeZones = timeZones;
        _user = user;
        _time = time;
    }

    public async Task<NotificationSettingsDto> GetSettingsAsync()
    {
        var settings = await _repo.GetSettingsAsync(_user.UserId);
        return ToDto(settings, await _profiles.GetAsync());
    }

    public async Task<NotificationSettingsDto> UpdateSettingsAsync(NotificationSettingsUpdateDto dto)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        // Older clients still send these here; they belong to the profile now
        var profile = dto.TimeZone is not null || dto.Language is not null
            ? await _profiles.UpdateAsync(new ProfileUpdateDto
            {
                TimeZone = dto.TimeZone,
                InterfaceLanguage = dto.Language is null
                    ? null
                    : NotificationOptions.Languages.Contains(dto.Language) ? dto.Language : NotificationOptions.DefaultLanguage,
            })
            : await _profiles.GetAsync();

        var settings = await _repo.GetSettingsAsync(_user.UserId);
        if (settings is null)
        {
            settings = new UserNotificationSettings
            {
                UserId = _user.UserId,
                // Legacy columns, kept in step with the profile
                TimeZone = profile.TimeZone ?? "UTC",
                Language = profile.InterfaceLanguage,
                CreatedAt = now,
            };
            await _repo.AddSettingsAsync(settings);
        }

        settings.EmailDailyReminder = dto.EmailDailyReminder ?? settings.EmailDailyReminder;
        settings.EmailWeeklyDigest = dto.EmailWeeklyDigest ?? settings.EmailWeeklyDigest;
        settings.EmailStreakReminder = dto.EmailStreakReminder ?? settings.EmailStreakReminder;

        // The address is taken from the signed in account when the user agrees to emails,
        // and forgotten when all emails are off
        if (settings.EmailDailyReminder || settings.EmailWeeklyDigest || settings.EmailStreakReminder)
        {
            settings.Email = _user.Email ?? throw new BadRequestException("Your account has no email address");
        }
        else
        {
            settings.Email = null;
        }

        settings.UpdatedAt = now;
        await _repo.SaveChangesAsync();

        return ToDto(settings, profile);
    }

    public async Task<NotificationFeedDto> GetFeedAsync()
    {
        var userId = _user.UserId;
        var timeZone = await _timeZones.GetAsync(userId);

        var today = await _activity.GetTodayAsync(userId, timeZone, _time.GetUtcNow().UtcDateTime);
        var notifications = await _repo.GetRecentAsync(userId, FeedSize);

        return new NotificationFeedDto
        {
            Today = today,
            Notifications = notifications.Select(ToDto).ToList(),
            UnreadCount = await _repo.CountUnreadAsync(userId),
        };
    }

    public async Task<bool> MarkReadAsync(Guid notificationId)
    {
        var notification = await _repo.GetAsync(notificationId, _user.UserId);
        if (notification is null) return false;

        notification.ReadAt ??= _time.GetUtcNow().UtcDateTime;
        await _repo.SaveChangesAsync();

        return true;
    }

    public Task MarkAllReadAsync() => _repo.MarkAllReadAsync(_user.UserId, _time.GetUtcNow().UtcDateTime);

    private static NotificationSettingsDto ToDto(UserNotificationSettings? settings, ProfileDto profile) => new()
    {
        TimeZone = profile.TimeZone,
        Language = profile.InterfaceLanguage,
        Email = settings?.Email,
        EmailDailyReminder = settings?.EmailDailyReminder ?? false,
        EmailWeeklyDigest = settings?.EmailWeeklyDigest ?? false,
        EmailStreakReminder = settings?.EmailStreakReminder ?? false,
    };

    private static NotificationDto ToDto(UserNotification notification) => new()
    {
        Id = notification.Id,
        Type = notification.Type,
        PeriodKey = notification.PeriodKey,
        Data = notification.Data.RootElement.Clone(),
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt,
    };
}
