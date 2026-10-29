namespace Application.Interfaces;

// Short-lived cache of what a user reads often and changes rarely (statistics).
// Every change the user makes drops their entries, so reads never lag behind their own actions
public interface IUserReadCache
{
    Task<T> GetOrCreateAsync<T>(Guid userId, string key, Func<Task<T>> factory, TimeSpan? ttl = null);

    void Invalidate(Guid userId);
}
