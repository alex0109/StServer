using Application.Constants;
using Application.DTOs.Flashcard;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;

namespace Application.Services;

public class FlashcardService : IFlashcardService
{
    private readonly IFlashcardRepository _flashcardRepository;
    private readonly IFlashcardScheduler _scheduler;
    private readonly IUserContext _user;

    public FlashcardService(
        IFlashcardRepository flashcardRepository,
        IFlashcardScheduler scheduler,
        IUserContext user)
    {
        _flashcardRepository = flashcardRepository;
        _scheduler = scheduler;
        _user = user;
    }

    public async Task<FlashcardStatsResponseDto?> GetStatsAsync(Guid dictionaryId)
    {
        var userId = _user.UserId;

        if (!await _flashcardRepository.DictionaryExistsAsync(dictionaryId, userId)) return null;

        var entries = await _flashcardRepository.GetActiveEntriesWithProgressAsync(dictionaryId, userId);
        var now = DateTime.UtcNow;

        return new FlashcardStatsResponseDto
        {
            Total = entries.Count,
            New = entries.Count(IsNew),
            Learning = entries.Count(e => e.Progress?.State == FlashcardState.Learning),
            Review = entries.Count(e => e.Progress?.State == FlashcardState.Review),
            Relearning = entries.Count(e => e.Progress?.State == FlashcardState.Relearning),
            Due = entries.Count(e => IsDue(e, now)),
        };
    }

    public async Task<FlashcardSessionResponseDto?> StartSessionAsync(Guid dictionaryId, FlashcardSessionCreateDto dto)
    {
        var userId = _user.UserId;

        if (!await _flashcardRepository.DictionaryExistsAsync(dictionaryId, userId)) return null;

        var entries = await _flashcardRepository.GetActiveEntriesWithProgressAsync(dictionaryId, userId);
        var now = DateTime.UtcNow;

        var cards = SelectCards(entries, dto, now);
        if (cards.Count == 0)
            throw new ConflictException("There are no cards to review");

        var session = FlashcardMapper.ToSessionEntity(dictionaryId, userId, dto.Mode, cards.Count, now);

        await _flashcardRepository.AddSessionAsync(session);
        await _flashcardRepository.SaveChangesAsync();

        return FlashcardMapper.ToSessionDto(session, cards);
    }

    public async Task<FlashcardSessionSummaryDto?> GetSessionAsync(Guid sessionId)
    {
        var session = await _flashcardRepository.GetSessionAsync(sessionId, _user.UserId);

        return session is null ? null : FlashcardMapper.ToSummaryDto(session);
    }

    public async Task<FlashcardSessionSummaryDto?> FinishSessionAsync(Guid sessionId)
    {
        var session = await _flashcardRepository.GetSessionAsync(sessionId, _user.UserId);
        if (session is null) return null;

        if (session.FinishedAt is null)
        {
            session.FinishedAt = DateTime.UtcNow;
            await _flashcardRepository.SaveChangesAsync();
        }

        return FlashcardMapper.ToSummaryDto(session);
    }

    public async Task<FlashcardReviewResponseDto?> AddReviewAsync(Guid sessionId, FlashcardReviewCreateDto dto)
    {
        var userId = _user.UserId;

        if (dto.Id == Guid.Empty)
            throw new BadRequestException("Review id is required");

        if (dto.Rating == ReviewRating.Manual)
            throw new BadRequestException("Manual rating can't be used in a session");

        var session = await _flashcardRepository.GetSessionAsync(sessionId, userId);
        if (session is null) return null;

        // Retried request: the review is already saved, return it as is
        var existing = await _flashcardRepository.GetReviewAsync(dto.Id, userId);
        if (existing is not null)
        {
            if (existing.SessionId != sessionId || existing.EntryId != dto.EntryId)
                throw new ConflictException("Review id is already used");

            var existingEntry = await _flashcardRepository.GetEntryWithProgressAsync(existing.EntryId, userId);

            return FlashcardMapper.ToReviewDto(existing, existingEntry?.Progress);
        }

        if (session.FinishedAt is not null)
            throw new ConflictException("Session is already finished");

        var entry = await _flashcardRepository.GetEntryWithProgressAsync(dto.EntryId, userId);
        if (entry is null || entry.DictionaryId != session.DictionaryId) return null;

        var now = DateTime.UtcNow;
        var before = FlashcardMapper.ToCard(entry.Progress, now);
        var after = _scheduler.Schedule(before, dto.Rating, now);

        var progress = entry.Progress;
        if (progress is null)
        {
            progress = FlashcardMapper.ToProgressEntity(entry.Id, userId, after, now);
            await _flashcardRepository.AddProgressAsync(progress);
        }
        else
        {
            FlashcardMapper.ApplyCard(progress, after, now);
        }

        var review = FlashcardMapper.ToReviewEntity(
            dto.Id, userId, sessionId, entry.Id, dto.Rating, dto.DurationMs, before, now);

        await _flashcardRepository.AddReviewAsync(review);
        await _flashcardRepository.SaveChangesAsync();

        return FlashcardMapper.ToReviewDto(review, progress);
    }

    public async Task<bool> UndoReviewAsync(Guid sessionId, Guid reviewId)
    {
        var userId = _user.UserId;

        var review = await _flashcardRepository.GetReviewAsync(reviewId, userId);
        if (review is null || review.SessionId != sessionId) return false;

        var session = await _flashcardRepository.GetSessionAsync(sessionId, userId);
        if (session is null) return false;

        if (session.FinishedAt is not null)
            throw new ConflictException("Session is already finished");

        // Restoring an older snapshot would wipe out the reviews made after it
        if (await _flashcardRepository.HasLaterReviewAsync(review))
            throw new ConflictException("Only the latest review of a card can be undone");

        var entry = await _flashcardRepository.GetEntryWithProgressAsync(review.EntryId, userId);
        var hadProgress = review.PrevReps > 0 || review.PrevLastReviewAt is not null;
        var now = DateTime.UtcNow;

        if (entry?.Progress is not null)
        {
            if (hadProgress)
                FlashcardMapper.ApplyCard(entry.Progress, FlashcardMapper.ToPrevCard(review), now);
            else
                _flashcardRepository.RemoveProgress(entry.Progress);
        }

        _flashcardRepository.RemoveReview(review);
        await _flashcardRepository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ResetProgressAsync(Guid entryId)
    {
        var userId = _user.UserId;

        var entry = await _flashcardRepository.GetEntryWithProgressAsync(entryId, userId);
        if (entry is null) return false;
        if (entry.Progress is null) return true;

        var now = DateTime.UtcNow;
        var before = FlashcardMapper.ToCard(entry.Progress, now);

        // Manual log keeps the review history consistent for FSRS
        var log = FlashcardMapper.ToReviewEntity(
            Guid.NewGuid(), userId, null, entry.Id, ReviewRating.Manual, null, before, now);

        await _flashcardRepository.AddReviewAsync(log);
        _flashcardRepository.RemoveProgress(entry.Progress);
        await _flashcardRepository.SaveChangesAsync();

        return true;
    }

    private static List<DictionaryEntry> SelectCards(
        List<DictionaryEntry> entries,
        FlashcardSessionCreateDto dto,
        DateTime now)
    {
        var defaultLimit = dto.Mode == FlashcardSessionMode.Due
            ? FlashcardLimits.DefaultSessionSize
            : FlashcardLimits.MaxSessionSize;
        var limit = Math.Clamp(dto.Limit ?? defaultLimit, 1, FlashcardLimits.MaxSessionSize);

        var selected = dto.Mode switch
        {
            // Most overdue cards first, new cards fill the rest
            FlashcardSessionMode.Due => entries
                .Where(e => IsDue(e, now))
                .OrderBy(e => e.Progress!.Due)
                .Concat(entries.Where(IsNew))
                .Take(limit)
                .ToArray(),
            FlashcardSessionMode.All => Shuffle(entries).Take(limit).ToArray(),
            FlashcardSessionMode.Custom => SelectCustom(entries, dto.EntryIds).Take(limit).ToArray(),
            _ => throw new BadRequestException("Unknown session mode"),
        };

        return Shuffle(selected);
    }

    private static IEnumerable<DictionaryEntry> SelectCustom(List<DictionaryEntry> entries, List<Guid>? entryIds)
    {
        if (entryIds is null || entryIds.Count == 0)
            throw new BadRequestException("EntryIds are required for Custom mode");

        var ids = entryIds.ToHashSet();

        return entries.Where(e => ids.Contains(e.Id));
    }

    private static List<DictionaryEntry> Shuffle(IEnumerable<DictionaryEntry> entries)
    {
        var result = entries.ToArray();
        Random.Shared.Shuffle(result);

        return result.ToList();
    }

    private static bool IsNew(DictionaryEntry entry) =>
        entry.Progress is null || entry.Progress.State == FlashcardState.New;

    private static bool IsDue(DictionaryEntry entry, DateTime now) =>
        !IsNew(entry) && entry.Progress!.Due <= now;
}
