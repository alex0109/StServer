using Application.Interfaces;
using Application.Mappers;
using Application.Schedulers;
using Domain.Entities;
using Domain.Utility.Review;

namespace Application.Services;

public class FlashcardProgressRebuilder : IFlashcardProgressRebuilder
{
    private const int BatchSize = 200;

    private readonly IFlashcardRepository _flashcardRepository;
    private readonly IFlashcardSchedulerProvider _schedulers;
    private readonly ILearningSettingsProvider? _learning;

    public FlashcardProgressRebuilder(
        IFlashcardRepository flashcardRepository,
        IFlashcardSchedulerProvider schedulers,
        ILearningSettingsProvider? learning = null)
    {
        _flashcardRepository = flashcardRepository;
        _schedulers = schedulers;
        _learning = learning;
    }

    public async Task RebuildEntryAsync(DictionaryEntry entry)
    {
        var reviews = await _flashcardRepository.GetReviewsForEntriesAsync([entry.Id]);

        await RebuildAsync(entry, reviews, _schedulers.Current, DateTime.UtcNow);
    }

    public async Task<int> RebuildOutdatedAsync()
    {
        var scheduler = _schedulers.Current;
        var processed = 0;
        var seen = new HashSet<Guid>();

        // Rebuilt cards leave the "outdated" filter, so the first batch is always the next one.
        // Seeing the same cards again means nothing changed, so stop instead of looping
        while (true)
        {
            var entries = await _flashcardRepository.GetEntriesWithOutdatedProgressAsync(
                scheduler.Algorithm, BatchSize);

            if (entries.Count == 0 || entries.All(x => !seen.Add(x.Id))) break;

            var reviews = await _flashcardRepository.GetReviewsForEntriesAsync(
                entries.Select(x => x.Id).ToList());
            var reviewsByEntry = reviews.ToLookup(x => x.EntryId);
            var now = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                await RebuildAsync(entry, reviewsByEntry[entry.Id].ToList(), scheduler, now);
            }

            await _flashcardRepository.SaveChangesAsync();

            processed += entries.Count;
        }

        return processed;
    }

    public async Task<int> RescheduleUserAsync(Guid userId)
    {
        var ids = await _flashcardRepository.GetEntryIdsWithProgressAsync(userId);

        foreach (var chunk in ids.Chunk(BatchSize))
        {
            var entries = await _flashcardRepository.GetEntriesWithProgressAsync(userId, chunk);
            var reviews = (await _flashcardRepository.GetReviewsForEntriesAsync(chunk.ToList())).ToLookup(x => x.EntryId);
            var now = DateTime.UtcNow;

            foreach (var entry in entries)
                await RebuildAsync(entry, reviews[entry.Id].ToList(), _schedulers.Current, now);

            await _flashcardRepository.SaveChangesAsync();
        }

        return ids.Count;
    }

    private async Task RebuildAsync(
        DictionaryEntry entry,
        List<FlashcardReview> reviews,
        IFlashcardScheduler scheduler,
        DateTime now)
    {
        // The history is replayed with the owner's current retention
        if (_learning is not null && reviews.Count > 0)
            scheduler = UserFlashcardScheduler.For(scheduler, (await _learning.GetAsync(reviews[0].UserId)).Overrides);

        var card = Replay(reviews, scheduler);

        if (card is null)
        {
            // Never reviewed or reset after the last answer, so the card is new again
            if (entry.Progress is not null)
                _flashcardRepository.RemoveProgress(entry.Progress);

            return;
        }

        if (entry.Progress is null)
        {
            entry.Progress = FlashcardMapper.ToProgressEntity(entry.Id, reviews[^1].UserId, card, now);
            entry.Progress.Algorithm = scheduler.Algorithm;
            await _flashcardRepository.AddProgressAsync(entry.Progress);
            return;
        }

        FlashcardMapper.ApplyCard(entry.Progress, card, now);
        entry.Progress.Algorithm = scheduler.Algorithm;
    }

    // Applies every answer in order and rewrites the "before" snapshot of each review,
    // so undo restores states produced by the same algorithm
    internal static FlashcardCard? Replay<TReview>(IEnumerable<TReview> reviews, IFlashcardScheduler scheduler)
        where TReview : IReviewLogEntry
    {
        FlashcardCard? card = null;

        foreach (var review in reviews.OrderBy(x => x.ReviewedAt).ThenBy(x => x.Id))
        {
            var before = card ?? FlashcardCard.CreateNew(review.ReviewedAt);

            FlashcardMapper.ApplyPrevCard(review, before);

            card = review.Rating == ReviewRating.Manual
                ? null
                : scheduler.Schedule(before, review.Rating, review.ReviewedAt);
        }

        return card;
    }
}
