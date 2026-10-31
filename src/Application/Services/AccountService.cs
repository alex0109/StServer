using Application.Account;
using Application.DTOs.Account;
using Application.Email;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Application.Observability;

namespace Application.Services;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _repo;
    private readonly IUserContext _user;
    private readonly IAccountJobs _jobs;
    private readonly IDeletedAccounts _deleted;
    private readonly IEmailSender _email;
    private readonly EmailOptions _emailOptions;
    private readonly TimeProvider _time;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        IAccountRepository repo,
        IUserContext user,
        IAccountJobs jobs,
        IDeletedAccounts deleted,
        IEmailSender email,
        EmailOptions emailOptions,
        TimeProvider time,
        ILogger<AccountService> logger)
    {
        _repo = repo;
        _user = user;
        _jobs = jobs;
        _deleted = deleted;
        _email = email;
        _emailOptions = emailOptions;
        _time = time;
        _logger = logger;
    }

    public async Task<AccountExportFile> ExportAsync()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var data = await _repo.GetExportAsync(_user.UserId, _user.Email, now);

        AppMetrics.AccountAction("export");
        return new AccountExportFile(AccountExportArchive.FileName(now), AccountExportArchive.Build(data));
    }

    public async Task DeleteAsync(AccountDeleteDto dto)
    {
        var userId = _user.UserId;

        // A second call for an account that is already gone does nothing
        if (_deleted.Contains(userId)) return;

        var email = _user.Email;
        if (email is null || !string.Equals(email, dto.ConfirmEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("The email doesn't match the account");
        }

        var now = _time.GetUtcNow();
        var signedInAt = AccountReauth.LastSignInAt(
            AccountReauth.AmrClaimTypes.SelectMany(_user.Claims),
            AccountReauth.AuthTimeClaimTypes.SelectMany(_user.Claims));

        if (!AccountReauth.IsFresh(signedInAt, now))
        {
            throw new ReauthRequiredException("Sign in again to delete the account");
        }

        var summary = await _repo.DeleteAsync(userId, now.UtcDateTime);
        _deleted.Add(userId);
        AppMetrics.AccountAction("delete");

        // Postgres is done; the sign in account goes next, with retries if Supabase is down
        _jobs.DeleteAuthUser(userId);

        // A paid plan would keep charging: its customer at the provider is closed too
        if (summary.BillingCustomer is { } customer)
        {
            _jobs.DeleteBillingCustomer(customer.Provider, customer.CustomerId);
        }

        // No email or full id in logs
        _logger.LogInformation(
            "Account {UserRef} deleted: {Deleted} rows removed, {Anonymized} anonymized",
            AccountIds.Short(userId), summary.RowsDeleted, summary.RowsAnonymized);

        // Not tied to the request: the data is already gone, a closed tab shouldn't stop the email
        await SendConfirmationAsync(email, AccountTexts.Language(dto.Language));
    }

    // Best effort: the account is deleted whether the email leaves or not
    private async Task SendConfirmationAsync(string email, string language)
    {
        if (!_email.IsConfigured) return;

        var text = AccountTexts.Deleted(language);
        var html = EmailTemplates.Layout(text.Title, string.Concat(text.Lines.Select(EmailTemplates.Paragraph)));

        try
        {
            await _email.SendAsync(
                new OutgoingEmail(
                    email,
                    text.Subject,
                    html,
                    string.Join("\n\n", text.Lines),
                    string.IsNullOrWhiteSpace(_emailOptions.FeedbackTo) ? null : _emailOptions.FeedbackTo));
        }
        catch (EmailUnavailableException exception)
        {
            _logger.LogWarning(exception, "Account deletion email was not sent");
        }
    }
}
