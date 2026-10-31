using System.Globalization;
using System.Text.Json;
using Application.DTOs.Notification;
using Application.Email;
using Application.Interfaces;
using Application.Statistics;
using Domain.Entities;
using Domain.Utility.Notification;
using Microsoft.Extensions.Logging;

namespace Application.Notifications;

// Makes reminders and digests for users whose local time has come. Every reminder is unique
// per user, kind and day (or week), so a repeated run never makes or sends a second one
public class NotificationGenerator : INotificationGenerator
{
    private const int PageSize = 200;

    private static readonly JsonSerializerOptions DataJson = new(JsonSerializerDefaults.Web);

    private readonly INotificationRepository _repo;
    private readonly IUserProfileRepository _profiles;
    private readonly IStudyActivityService _activity;
    private readonly IEmailSender _emailSender;
    private readonly NotificationOptions _options;
    private readonly EmailOptions _emailOptions;
    private readonly TimeProvider _time;
    private readonly ILogger<NotificationGenerator> _logger;

    public NotificationGenerator(
        INotificationRepository repo,
        IUserProfileRepository profiles,
        IStudyActivityService activity,
        IEmailSender emailSender,
        NotificationOptions options,
        EmailOptions emailOptions,
        TimeProvider time,
        ILogger<NotificationGenerator> logger)
    {
        _repo = repo;
        _profiles = profiles;
        _activity = activity;
        _emailSender = emailSender;
        _options = options;
        _emailOptions = emailOptions;
        _time = time;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var skip = 0;
        var created = 0;

        while (true)
        {
            var page = await _profiles.GetRecipientsPageAsync(skip, PageSize);
            if (page.Count == 0) break;

            foreach (var settings in page)
            {
                try
                {
                    created += await GenerateForUserAsync(settings, now);
                }
                catch (Exception exception)
                {
                    // One user's problem must not stop reminders for everyone else
                    _logger.LogError(exception, "Reminders failed for user {UserId}", settings.UserId);
                }
            }

            skip += page.Count;
        }

        _logger.LogInformation("Reminder job made {Count} reminders", created);
    }

    internal async Task<int> GenerateForUserAsync(NotificationRecipient settings, DateTime now)
    {
        var timeZone = UserTimeZone.Resolve(settings.TimeZone);
        var local = UserTimeZone.ToLocal(now, timeZone);
        var today = DateOnly.FromDateTime(local);
        var created = 0;

        // The period keys stay per day and week, so a changed hour never sends a second reminder
        if (InWindow(local, settings.DailyReminderHour ?? _options.DailyReminderHour))
        {
            var key = today.ToString("yyyy-MM-dd");
            if (!await _repo.ExistsAsync(settings.UserId, NotificationType.DailyReview, key))
            {
                var todayData = await _activity.GetTodayAsync(settings.UserId, timeZone, now);
                if (todayData.DueWords > 0 || todayData.DueQuestions > 0)
                {
                    var data = new DailyReviewData(todayData.DueWords, todayData.NewWords, todayData.Dictionaries, todayData.DueQuestions);
                    created += await CreateAsync(settings, NotificationType.DailyReview, key, data, now,
                        settings.EmailDailyReminder ? NotificationTexts.DailyReview(settings.Language, data) : null);
                }
            }
        }

        if (InWindow(local, settings.StreakReminderHour ?? _options.StreakReminderHour))
        {
            var key = today.ToString("yyyy-MM-dd");
            if (!await _repo.ExistsAsync(settings.UserId, NotificationType.StreakAtRisk, key))
            {
                var streak = await _activity.GetStreakAsync(settings.UserId, timeZone, now);
                if (streak.CurrentStreakDays > 0 && !streak.StudiedToday)
                {
                    var data = new StreakAtRiskData(streak.CurrentStreakDays);
                    created += await CreateAsync(settings, NotificationType.StreakAtRisk, key, data, now,
                        settings.EmailStreakReminder ? NotificationTexts.StreakAtRisk(settings.Language, data) : null);
                }
            }
        }

        if (local.DayOfWeek == _options.WeeklyDigestDay && InWindow(local, _options.WeeklyDigestHour))
        {
            // The digest is about the week that just ended
            var lastWeekDay = today.AddDays(-1).ToDateTime(TimeOnly.MinValue);
            var key = $"{ISOWeek.GetYear(lastWeekDay)}-W{ISOWeek.GetWeekOfYear(lastWeekDay):00}";

            if (!await _repo.ExistsAsync(settings.UserId, NotificationType.WeeklyDigest, key))
            {
                var data = await _activity.GetWeekAsync(settings.UserId, timeZone, today, now);
                if (data.HasActivity)
                {
                    created += await CreateAsync(settings, NotificationType.WeeklyDigest, key, data, now,
                        settings.EmailWeeklyDigest ? NotificationTexts.WeeklyDigest(settings.Language, data) : null);
                }
            }
        }

        return created;
    }

    private bool InWindow(DateTime local, int hour) =>
        local.Hour >= hour && local.Hour < hour + _options.WindowHours;

    private async Task<int> CreateAsync<T>(
        NotificationRecipient settings,
        NotificationType type,
        string periodKey,
        T data,
        DateTime now,
        NotificationTexts.EmailText? email)
    {
        var notification = new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = settings.UserId,
            Type = type,
            PeriodKey = periodKey,
            Data = JsonSerializer.SerializeToDocument(data, DataJson),
            CreatedAt = now,
        };

        // The row is the "already sent" mark: if another run made it first, nothing is sent
        if (!await _repo.TryAddAsync(notification)) return 0;

        if (email is not null && settings.Email is not null && _emailSender.IsConfigured)
        {
            try
            {
                await _emailSender.SendAsync(BuildEmail(settings, email));
                notification.EmailSentAt = _time.GetUtcNow().UtcDateTime;
                await _repo.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                // Not retried: a late reminder is worse than a missing one, the in-app one stays
                _logger.LogWarning(exception, "Reminder email {Type} failed for user {UserId}", type, settings.UserId);
            }
        }

        return 1;
    }

    private OutgoingEmail BuildEmail(NotificationRecipient settings, NotificationTexts.EmailText text)
    {
        var url = $"{_emailOptions.AppUrl.TrimEnd('/')}{text.Path}";
        var body = string.Concat(text.Lines.Select(EmailTemplates.Paragraph)) + EmailTemplates.Button(text.Button, url);
        var footer =
            $"{EmailTemplates.Encode(NotificationTexts.Footer(settings.Language))} " +
            $"<a href=\"{EmailTemplates.Encode(url)}\" style=\"color:#525252\">{EmailTemplates.Encode(NotificationTexts.ManageLink(settings.Language))}</a>";

        return new OutgoingEmail(
            settings.Email!,
            text.Subject,
            EmailTemplates.Layout(text.Title, body, footer),
            $"{string.Join("\n", text.Lines)}\n\n{text.Button}: {url}\n\n{NotificationTexts.Footer(settings.Language)}");
    }
}
