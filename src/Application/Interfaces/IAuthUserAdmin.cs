namespace Application.Interfaces;

// The sign in provider's side of an account (Supabase Auth)
public interface IAuthUserAdmin
{
    bool IsConfigured { get; }

    // A user that is already gone counts as deleted
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
