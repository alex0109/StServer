using Domain.Entities;

namespace Application.Interfaces;

public interface IFlashcardProgressRebuilder
{
    // Replays the review history of one entry with the current algorithm, does not save
    Task RebuildEntryAsync(DictionaryEntry entry);

    // Rebuilds every card scheduled by another algorithm, returns how many were processed
    Task<int> RebuildOutdatedAsync();

    // Replays every word of the user with their current settings, e.g. a new retention. Saves
    Task<int> RescheduleUserAsync(Guid userId);
}
