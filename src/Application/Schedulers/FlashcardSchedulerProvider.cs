using Application.Interfaces;
using Domain.Utility.Flashcard;

namespace Application.Schedulers;

public class FlashcardSchedulerProvider : IFlashcardSchedulerProvider
{
    private readonly Dictionary<FlashcardAlgorithm, IFlashcardScheduler> _schedulers;
    private readonly FlashcardSchedulerOptions _options;

    public FlashcardSchedulerProvider(
        IEnumerable<IFlashcardScheduler> schedulers,
        FlashcardSchedulerOptions options)
    {
        _schedulers = schedulers.ToDictionary(x => x.Algorithm);
        _options = options;

        if (!_schedulers.ContainsKey(options.Algorithm))
            throw new InvalidOperationException($"Flashcard scheduler {options.Algorithm} is not registered");
    }

    public IFlashcardScheduler Current => _schedulers[_options.Algorithm];

    public IFlashcardScheduler Get(FlashcardAlgorithm algorithm) =>
        _schedulers.TryGetValue(algorithm, out var scheduler)
            ? scheduler
            : throw new InvalidOperationException($"Flashcard scheduler {algorithm} is not registered");
}
