using Application.Email;
using Application.Exceptions;
using Application.Interfaces;
using Resend;

namespace Infrastructure.Email;

// Emails through Resend. The key comes only from the RESEND_API_KEY environment variable
public sealed class ResendEmailSender : IEmailSender
{
    public const string HttpClientName = "Resend";
    public const string ApiKeyVariable = "RESEND_API_KEY";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmailOptions _options;
    private readonly string? _apiKey;

    public ResendEmailSender(IHttpClientFactory httpClientFactory, EmailOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;

        var key = Environment.GetEnvironmentVariable(ApiKeyVariable)?.Trim();
        _apiKey = string.IsNullOrEmpty(key) ? null : key;
    }

    public bool IsConfigured => _apiKey is not null;

    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        if (_apiKey is null) throw new EmailUnavailableException("Email is not configured");

        var client = ResendClient.Create(
            new ResendClientOptions { ApiToken = _apiKey },
            _httpClientFactory.CreateClient(HttpClientName));

        var message = new EmailMessage
        {
            From = _options.From,
            To = email.To,
            Subject = email.Subject,
            HtmlBody = email.Html,
            TextBody = email.Text,
        };

        if (email.ReplyTo is not null) message.ReplyTo = email.ReplyTo;

        try
        {
            await client.EmailSendAsync(message, cancellationToken);
        }
        catch (ResendException exception)
        {
            // Only the status and error type, the message may quote the email
            throw new EmailUnavailableException(
                $"Resend error {(int?)exception.StatusCode} {exception.ErrorType}", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new EmailUnavailableException("Resend connection error", exception);
        }
    }
}
