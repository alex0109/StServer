using System.Text.Json;
using Application.DTOs.Notification;
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
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public NotificationService(
        INotificationRepository repo,
        IStudyActivityService activity,
        IUserContext user,
        TimeProvider time)
    {
        _repo = repo;
        _activity = activity;
        _user = user;
        _time = time;
    }

    public async Task<NotificationSettingsDto> GetSettingsAsync()
    {
        var settings = await _repo.GetSettingsAsync(_user.UserId);
        return ToDto(settings);
    }

    public async Task<NotificationSettingsDto> UpdateSettingsAsync(NotificationSettingsUpdateDto dto)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var settings = await _repo.GetSettingsAsync(_user.UserId);

        if (dto.TimeZone is not null && !UserTimeZone.TryResolve(dto.TimeZone, out _))
            throw new BadRequestException("Unknown time zone");

        if (settings is null)
        {
            if (dto.TimeZone is null)
                throw new BadRequestException("Time zone is required");

            settings = new UserNotificationSettings
            {
                UserId = _user.UserId,
                TimeZone = dto.TimeZone,
                Language = NotificationOptions.DefaultLanguage,
                CreatedAt = now,
            };
            await _repo.AddSettingsAsync(settings);
        }

        if (dto.TimeZone is not null) settings.TimeZone = dto.TimeZone;
        if (dto.Language is not null)
        {
            settings.Language = NotificationOptions.Languages.Contains(dto.Language)
                ? dto.Language
                : NotificationOptions.DefaultLanguage;
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

        return ToDto(settings);
    }

    public async Task<NotificationFeedDto> GetFeedAsync()
    {
        var userId = _user.UserId;
        var settings = await _repo.GetSettingsAsync(userId);
        var timeZone = UserTimeZone.Resolve(settings?.TimeZone);

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

    private static NotificationSettingsDto ToDto(UserNotificationSettings? settings) => new()
    {
        TimeZone = settings?.TimeZone,
        Language = settings?.Language ?? NotificationOptions.DefaultLanguage,
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
