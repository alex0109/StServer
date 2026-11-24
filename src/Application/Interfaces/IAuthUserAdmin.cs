namespace Application.Interfaces;

// The sign in provider's side of an account (Supabase Auth)
public interface IAuthUserAdmin
{
    bool IsConfigured { get; }

    // A user that is already gone counts as deleted
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);

    // Sign-in emails for the admin panel. Users that can't be looked up are left out, so the
    // answer may be empty; it never fails
    Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
