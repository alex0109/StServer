using Application.DTOs.Flashcard;
using Application.Schedulers;
using Domain.Entities;
using Domain.Utility.Flashcard;
using Domain.Utility.Review;

namespace Application.Mappers;

public static class FlashcardMapper
{
    public static FlashcardCard ToCard(DictionaryEntryProgress? progress, DateTime now)
    {
        if (progress is null) return FlashcardCard.CreateNew(now);

        return new FlashcardCard
        {
            State = progress.State,
            Due = progress.Due,
            Stability = progress.Stability,
            Difficulty = progress.Difficulty,
            ScheduledDays = progress.ScheduledDays,
            LearningSteps = progress.LearningSteps,
            Reps = progress.Reps,
            Lapses = progress.Lapses,
            LastReviewAt = progress.LastReviewAt,
        };
    }

    public static FlashcardCard ToPrevCard(FlashcardReview review)
    {
        return new FlashcardCard
        {
            State = review.PrevState,
            Due = review.PrevDue,
            Stability = review.PrevStability,
            Difficulty = review.PrevDifficulty,
            ScheduledDays = review.PrevScheduledDays,
            LearningSteps = review.PrevLearningSteps,
            Reps = review.PrevReps,
            Lapses = review.PrevLapses,
            LastReviewAt = review.PrevLastReviewAt,
        };
    }

    public static DictionaryEntryProgress ToProgressEntity(Guid entryId, Guid userId, FlashcardCard card, DateTime now)
    {
        var progress = new DictionaryEntryProgress
        {
            EntryId = entryId,
            UserId = userId,
            CreatedAt = now,
        };

        ApplyCard(progress, card, now);

        return progress;
    }

    public static void ApplyCard(DictionaryEntryProgress progress, FlashcardCard card, DateTime now)
    {
        progress.State = card.State;
        progress.Due = card.Due;
        progress.Stability = card.Stability;
        progress.Difficulty = card.Difficulty;
        progress.ScheduledDays = card.ScheduledDays;
        progress.LearningSteps = card.LearningSteps;
        progress.Reps = card.Reps;
        progress.Lapses = card.Lapses;
        progress.LastReviewAt = card.LastReviewAt;
        progress.UpdatedAt = now;
    }

    public static FlashcardProgressResponseDto ToProgressDto(DictionaryEntryProgress progress)
    {
        return new FlashcardProgressResponseDto
        {
            State = progress.State,
            Due = progress.Due,
            Stability = progress.Stability,
            Difficulty = progress.Difficulty,
            ScheduledDays = progress.ScheduledDays,
            LearningSteps = progress.LearningSteps,
            Reps = progress.Reps,
            Lapses = progress.Lapses,
            LastReviewAt = progress.LastReviewAt,
        };
    }

    public static FlashcardReview ToReviewEntity(
        Guid id,
        Guid userId,
        Guid? sessionId,
        Guid entryId,
        ReviewRating rating,
        int? durationMs,
        FlashcardCard before,
        DateTime now)
    {
        return new FlashcardReview
        {
            Id = id,
            UserId = userId,
            SessionId = sessionId,
            EntryId = entryId,
            Rating = rating,
            ReviewedAt = now,
            DurationMs = durationMs,
            ElapsedDays = before.LastReviewAt is DateTime lastReviewAt
                ? Math.Max(0, (now - lastReviewAt).TotalDays)
                : 0,
            PrevState = before.State,
            PrevDue = before.Due,
            PrevStability = before.Stability,
            PrevDifficulty = before.Difficulty,
            PrevScheduledDays = before.ScheduledDays,
            PrevLearningSteps = before.LearningSteps,
            PrevReps = before.Reps,
            PrevLapses = before.Lapses,
            PrevLastReviewAt = before.LastReviewAt,
        };
    }

    public static FlashcardReviewResponseDto ToReviewDto(FlashcardReview review, DictionaryEntryProgress? progress)
    {
        return new FlashcardReviewResponseDto
        {
            Id = review.Id,
            SessionId = review.SessionId,
            EntryId = review.EntryId,
            Rating = review.Rating,
            ReviewedAt = review.ReviewedAt,
            Progress = progress is null ? null : ToProgressDto(progress),
        };
    }

    public static FlashcardSession ToSessionEntity(
        Guid dictionaryId,
        Guid userId,
        FlashcardSessionMode mode,
        int cardsTotal,
        DateTime now)
    {
        return new FlashcardSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DictionaryId = dictionaryId,
            Mode = mode,
            CardsTotal = cardsTotal,
            StartedAt = now,
        };
    }

    public static FlashcardSessionResponseDto ToSessionDto(FlashcardSession session, IEnumerable<DictionaryEntry> cards)
    {
        return new FlashcardSessionResponseDto
        {
            Id = session.Id,
            DictionaryId = session.DictionaryId,
            Mode = session.Mode,
            CardsTotal = session.CardsTotal,
            Cards = cards.Select(DictionaryMapper.ToEntryDto).ToList(),
            StartedAt = session.StartedAt,
            FinishedAt = session.FinishedAt,
        };
    }

    public static FlashcardSessionSummaryDto ToSummaryDto(FlashcardSession session)
    {
        return new FlashcardSessionSummaryDto
        {
            Id = session.Id,
            DictionaryId = session.DictionaryId,
            Mode = session.Mode,
            CardsTotal = session.CardsTotal,
            Reviewed = session.Reviews.Count,
            Again = session.Reviews.Count(r => r.Rating == ReviewRating.Again),
            Hard = session.Reviews.Count(r => r.Rating == ReviewRating.Hard),
            Good = session.Reviews.Count(r => r.Rating == ReviewRating.Good),
            Easy = session.Reviews.Count(r => r.Rating == ReviewRating.Easy),
            StartedAt = session.StartedAt,
            FinishedAt = session.FinishedAt,
        };
    }
}
