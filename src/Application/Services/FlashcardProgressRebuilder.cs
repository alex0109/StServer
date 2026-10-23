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

    public FlashcardProgressRebuilder(
        IFlashcardRepository flashcardRepository,
        IFlashcardSchedulerProvider schedulers)
    {
        _flashcardRepository = flashcardRepository;
        _schedulers = schedulers;
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

    private async Task RebuildAsync(
        DictionaryEntry entry,
        List<FlashcardReview> reviews,
        IFlashcardScheduler scheduler,
        DateTime now)
    {
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
    internal static FlashcardCard? Replay(IEnumerable<FlashcardReview> reviews, IFlashcardScheduler scheduler)
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
