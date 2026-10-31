using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Application.Constants;
using Application.DTOs.Feedback;
using Domain.Entities;
using Application.Email;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class FeedbackService : IFeedbackService
{
    public const int MinMessageLength = 10;
    public const int MaxMessageLength = 3000;
    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 254;

    // Per sender and calendar month (UTC). Not shown anywhere: messages above it are
    // answered like any other, they just aren't sent
    public const int MaxPerMonth = 10;

    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _options;
    private readonly IUserContext _user;
    private readonly IFeedbackLogRepository _log;
    private readonly ILimitLock _limitLock;
    private readonly TimeProvider _time;
    private readonly ILogger<FeedbackService> _logger;

    public FeedbackService(
        IEmailSender emailSender,
        EmailOptions options,
        IUserContext user,
        IFeedbackLogRepository log,
        ILimitLock limitLock,
        TimeProvider time,
        ILogger<FeedbackService> logger)
    {
        _emailSender = emailSender;
        _options = options;
        _user = user;
        _log = log;
        _limitLock = limitLock;
        _time = time;
        _logger = logger;
    }

    public async Task SendAsync(FeedbackCreateDto dto, CancellationToken cancellationToken = default)
    {
        // A filled hidden field means a bot: it gets the usual answer and nothing is sent
        if (!string.IsNullOrWhiteSpace(dto.Website))
        {
            _logger.LogInformation("Feedback from a bot was dropped");
            return;
        }

        var message = dto.Message?.Trim() ?? string.Empty;
        if (message.Length is < MinMessageLength or > MaxMessageLength)
            throw new BadRequestException($"Message must be {MinMessageLength} to {MaxMessageLength} characters long");

        var name = string.IsNullOrWhiteSpace(dto.Name) ? null : EmailTemplates.SingleLine(dto.Name, MaxNameLength);
        var email = NormalizeEmail(dto.Email);

        if (!_emailSender.IsConfigured || string.IsNullOrWhiteSpace(_options.FeedbackTo))
            throw new EmailUnavailableException("Feedback can't be sent right now, please try again later");

        var details = new List<(string Label, string? Value)>
        {
            ("Category", dto.Category.ToString()),
            ("Name", name),
            ("Email", email),
            ("User id", _user.CurrentUserId?.ToString()),
        };

        var detailsHtml = string.Concat(details
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .Select(x => $"<p style=\"margin:0 0 4px;font-size:13px;color:#525252\"><b>{x.Label}:</b> {EmailTemplates.Encode(x.Value)}</p>"));

        var messageHtml =
            $"<div style=\"margin-top:16px;padding:16px;background:#fafafa;border-radius:12px;white-space:pre-wrap;font-size:15px;line-height:1.5\">{EmailTemplates.Encode(message)}</div>";

        var subject = $"Studytracker feedback · {dto.Category}" + (name is null ? "" : $" · {name}");

        // Count, send and record under one lock, so parallel requests can't slip past the limit
        var senderKey = SenderKey();
        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.Feedback, ToLockOwner(senderKey));

        var now = _time.GetUtcNow().UtcDateTime;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (await _log.CountSinceAsync(senderKey, monthStart) >= MaxPerMonth)
        {
            _logger.LogInformation("Feedback over the monthly limit was dropped for {Sender}", senderKey);
            return;
        }

        await _emailSender.SendAsync(new OutgoingEmail(
            _options.FeedbackTo,
            EmailTemplates.SingleLine(subject, 150),
            EmailTemplates.Layout("New feedback", detailsHtml + messageHtml),
            $"{string.Join('\n', details.Where(x => !string.IsNullOrEmpty(x.Value)).Select(x => $"{x.Label}: {x.Value}"))}\n\n{message}",
            email), cancellationToken);

        // Only messages that really went out count
        await _log.AddAsync(new FeedbackSubmission { Id = Guid.NewGuid(), SenderKey = senderKey, CreatedAt = now });
        await _log.SaveChangesAsync();
        await limitLock.CommitAsync();
    }

    // Signed in users are counted by account, guests by a hash of their address
    private string SenderKey()
    {
        if (_user.CurrentUserId is Guid userId) return $"user:{userId}";
        if (string.IsNullOrWhiteSpace(_user.ClientIp)) return "guest";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"studytracker-feedback:{_user.ClientIp}"));
        return $"ip:{Convert.ToHexString(hash, 0, 16)}";
    }

    private static Guid ToLockOwner(string senderKey) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(senderKey)).AsSpan(0, 16));

    private static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var email = value.Trim();
        if (email.Length > MaxEmailLength || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new BadRequestException("Email is not valid");

        return email;
    }
}
