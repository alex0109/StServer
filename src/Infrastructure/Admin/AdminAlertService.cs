using Application.Email;
using Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Admin;

// Emails to the owner. An alert must never break the request it is about, so sending errors are only logged
public class AdminAlertService : IAdminAlerts
{
    private static readonly TimeSpan SignInMemory = TimeSpan.FromHours(24);
    // One "denied" email per caller per hour: enough to notice, not a mailbox flood
    private static readonly TimeSpan DeniedThrottle = TimeSpan.FromHours(1);

    private readonly IEmailSender _email;
    private readonly EmailOptions _options;
    private readonly IUserContext _user;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;
    private readonly ILogger<AdminAlertService> _logger;

    public AdminAlertService(
        IEmailSender email,
        EmailOptions options,
        IUserContext user,
        IMemoryCache cache,
        TimeProvider time,
        ILogger<AdminAlertService> logger)
    {
        _email = email;
        _options = options;
        _user = user;
        _cache = cache;
        _time = time;
        _logger = logger;
    }

    public Task SignedInAsync(string? sessionId)
    {
        var key = $"admin-alert:signin:{sessionId ?? $"{_user.CurrentUserId}:{_user.ClientIp}"}";
        if (_cache.TryGetValue(key, out _)) return Task.CompletedTask;
        _cache.Set(key, true, SignInMemory);

        _logger.LogInformation("Admin {UserId} signed in from {Ip}", _user.CurrentUserId, _user.ClientIp);

        return SendAsync(
            "Вхід в адмінку Studytracker",
            "Щойно хтось увійшов в адмінку. Якщо це не ти, одразу завершуй усі сесії в Supabase і зміни пароль Google.");
    }

    public Task AccessDeniedAsync(string reason)
    {
        var key = $"admin-alert:denied:{_user.CurrentUserId?.ToString() ?? _user.ClientIp}";
        if (_cache.TryGetValue(key, out _)) return Task.CompletedTask;
        _cache.Set(key, true, DeniedThrottle);

        _logger.LogWarning("Admin access denied for {UserId} from {Ip}: {Reason}", _user.CurrentUserId, _user.ClientIp, reason);

        return SendAsync(
            "Спроба доступу до адмінки Studytracker",
            $"Запит до адмін-API відхилено: {reason}. Наступні спроби з цього акаунта протягом години окремо не надсилаються.");
    }

    private async Task SendAsync(string subject, string text)
    {
        var to = string.IsNullOrWhiteSpace(_options.AdminAlertsTo) ? _options.FeedbackTo : _options.AdminAlertsTo;
        if (!_email.IsConfigured || string.IsNullOrWhiteSpace(to)) return;

        var details = new (string Label, string? Value)[]
        {
            ("Час (UTC)", _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")),
            ("Акаунт", _user.Email),
            ("User id", _user.CurrentUserId?.ToString()),
            ("IP", _user.ClientIp),
            ("Браузер", _user.UserAgent),
        };

        var rows = string.Concat(details
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .Select(x => $"<p style=\"margin:0 0 4px;font-size:13px;color:#525252\"><b>{x.Label}:</b> {EmailTemplates.Encode(x.Value)}</p>"));

        try
        {
            await _email.SendAsync(new OutgoingEmail(
                to,
                EmailTemplates.SingleLine(subject, 150),
                EmailTemplates.Layout(subject, EmailTemplates.Paragraph(text) + rows),
                $"{text}\n\n{string.Join('\n', details.Where(x => !string.IsNullOrEmpty(x.Value)).Select(x => $"{x.Label}: {x.Value}"))}",
                null));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Admin alert email could not be sent");
        }
    }
}
