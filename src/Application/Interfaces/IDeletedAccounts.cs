namespace Application.Interfaces;

// Access tokens stay valid until they expire, even after the Supabase user is gone.
// Requests with a deleted account are refused, so they can't create new rows
public interface IDeletedAccounts
{
    void Add(Guid userId);

    bool Contains(Guid userId);
}
