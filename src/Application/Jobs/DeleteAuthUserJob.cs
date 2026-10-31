using Application.Account;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Jobs;

// Second half of an account deletion: the Supabase user. A failure throws, so Hangfire retries
// with growing delays; the study data is already gone by then
public class DeleteAuthUserJob
{
    private readonly IAuthUserAdmin _auth;
    private readonly ILogger<DeleteAuthUserJob> _logger;

    public DeleteAuthUserJob(IAuthUserAdmin auth, ILogger<DeleteAuthUserJob> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    public async Task Run(Guid userId)
    {
        if (!_auth.IsConfigured)
        {
            throw new InvalidOperationException("SUPABASE_SERVICE_ROLE_KEY is not set, the sign in account can't be deleted");
        }

        await _auth.DeleteUserAsync(userId);
        _logger.LogInformation("Sign in account {UserRef} deleted", AccountIds.Short(userId));
    }
}
