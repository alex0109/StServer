using Application.Interfaces;

namespace Application.Jobs;

// Moves cards to the configured algorithm after switching it, by replaying their review history
public class FlashcardRescheduleJob
{
    private readonly IFlashcardProgressRebuilder _progressRebuilder;

    public FlashcardRescheduleJob(IFlashcardProgressRebuilder progressRebuilder)
    {
        _progressRebuilder = progressRebuilder;
    }

    public async Task RescheduleOutdated()
    {
        await _progressRebuilder.RebuildOutdatedAsync();
    }
}
