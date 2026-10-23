using Application.Constants;

namespace Application.Interfaces;

// Serializes "count, check limit, insert" for one owner, so parallel requests
// can't both pass the check and end up above the limit
public interface ILimitLock
{
    // Owner is the user for user wide limits, or the parent (material, dictionary) for nested ones
    Task<ILimitLockScope> AcquireAsync(LimitScope scope, Guid ownerId);
}

public interface ILimitLockScope : IAsyncDisposable
{
    // Saves the work done under the lock, disposing without commit rolls it back
    Task CommitAsync();
}
