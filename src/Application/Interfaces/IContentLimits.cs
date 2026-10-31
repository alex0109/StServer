using Application.Limits;

namespace Application.Interfaces;

// The limits of a user's plan, and the checks every create goes through
public interface IContentLimits
{
    Task<TierLimits> ForUserAsync(Guid userId);

    // Throws LimitExceededException when there is no room for one more material.
    // A library copy is checked against the library limit unless imports share the material limit
    Task EnsureMaterialPlaceAsync(Guid userId, bool libraryCopy);

    bool CountLibraryImports { get; }
}
