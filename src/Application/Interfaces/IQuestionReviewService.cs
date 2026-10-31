using Domain.Entities;
using Domain.Utility.Review;

namespace Application.Interfaces;

// Spaced repetition of questions: every finished attempt is one review per answered question
public interface IQuestionReviewService
{
    // Adds the reviews of a just finished attempt, does not save: the caller saves with the attempt
    Task ScheduleFinishedAttemptAsync(Attempt attempt);

    // One answer in a "Today" review session, scheduled right away. Does not save
    Task<QuestionProgress> ReviewInSessionAsync(Question question, Guid sessionId, ReviewRating rating);

    // The answer of a finished attempt was re-graded: its review gets the new rating and the
    // question's history is replayed. Does not save
    Task ReRateAttemptAnswerAsync(Guid attemptId, Guid questionId, Guid userId, ReviewRating rating);

    // Replays the whole log of one question, used after its answer was re-graded. Does not save
    Task RebuildQuestionAsync(Guid questionId, Guid userId);

    // Reviews for attempts finished before scheduling existed; safe to run again. Returns new reviews
    Task<int> BackfillAsync();

    // Moves questions to the configured algorithm, returns how many were processed
    Task<int> RebuildOutdatedAsync();

    // Replays every question of the user with their current settings, e.g. a new retention. Saves
    Task<int> RescheduleUserAsync(Guid userId);
}
