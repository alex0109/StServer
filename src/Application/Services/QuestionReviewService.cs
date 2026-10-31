using Application.Interfaces;
using Application.Observability;
using Application.Mappers;
using Application.Schedulers;
using Domain.Entities;
using Domain.Utility.Review;

namespace Application.Services;

public class QuestionReviewService : IQuestionReviewService
{
    private const int BatchSize = 200;

    private readonly IQuestionReviewRepository _repo;
    private readonly IFlashcardSchedulerProvider _schedulers;
    private readonly ILearningSettingsProvider? _learning;

    public QuestionReviewService(
        IQuestionReviewRepository repo,
        IFlashcardSchedulerProvider schedulers,
        ILearningSettingsProvider? learning = null)
    {
        _repo = repo;
        _schedulers = schedulers;
        _learning = learning;
    }

    public async Task ScheduleFinishedAttemptAsync(Attempt attempt)
    {
        if (attempt.Results.Count == 0) return;

        var reviewed = await _repo.GetReviewedQuestionIdsAsync(attempt.Id);
        var pending = attempt.Results
            .Where(r => !reviewed.Contains(r.QuestionId))
            .OrderBy(AnsweredAt)
            .ToList();
        if (pending.Count == 0) return;

        var questions = (await _repo.GetQuestionsWithProgressAsync(
                pending.Select(r => r.QuestionId).Distinct().ToList(), attempt.UserId))
            .ToDictionary(q => q.Id);
        var scheduler = await ForUserAsync(attempt.UserId);
        var now = DateTime.UtcNow;

        foreach (var result in pending)
        {
            if (!questions.TryGetValue(result.QuestionId, out var question)) continue;

            // Scheduled by another algorithm and the background job has not reached it yet
            if (question.Progress is not null && question.Progress.Algorithm != scheduler.Algorithm)
                await RebuildAsync(question, await _repo.GetReviewsForQuestionsAsync([question.Id]), scheduler, now);

            var review = ToReview(result, attempt.Id);
            var before = FlashcardMapper.ToCard(question.Progress, review.ReviewedAt);

            // The log stays in order even if an answer time is older than the last review
            if (before.LastReviewAt is DateTime last && review.ReviewedAt < last)
                review.ReviewedAt = last;

            FlashcardMapper.ApplyPrevCard(review, before);
            var after = scheduler.Schedule(before, review.Rating, review.ReviewedAt);

            if (question.Progress is null)
            {
                question.Progress = QuestionReviewMapper.ToProgressEntity(question.Id, attempt.UserId, after, now);
                await _repo.AddProgressAsync(question.Progress);
            }
            else
            {
                FlashcardMapper.ApplyCard(question.Progress, after, now);
            }

            question.Progress.Algorithm = scheduler.Algorithm;
            await _repo.AddReviewAsync(review);
            AppMetrics.Review("question", "attempt");
        }
    }

    public async Task<QuestionProgress> ReviewInSessionAsync(Question question, Guid sessionId, ReviewRating rating)
    {
        var scheduler = await ForUserAsync(question.UserId);
        var now = DateTime.UtcNow;

        if (question.Progress is not null && question.Progress.Algorithm != scheduler.Algorithm)
            await RebuildAsync(question, await _repo.GetReviewsForQuestionsAsync([question.Id]), scheduler, now);

        var review = new QuestionReview
        {
            Id = Guid.NewGuid(),
            UserId = question.UserId,
            QuestionId = question.Id,
            SessionId = sessionId,
            Rating = rating,
            ReviewedAt = now,
        };

        var before = FlashcardMapper.ToCard(question.Progress, now);
        FlashcardMapper.ApplyPrevCard(review, before);
        var after = scheduler.Schedule(before, rating, now);

        if (question.Progress is null)
        {
            question.Progress = QuestionReviewMapper.ToProgressEntity(question.Id, question.UserId, after, now);
            await _repo.AddProgressAsync(question.Progress);
        }
        else
        {
            FlashcardMapper.ApplyCard(question.Progress, after, now);
        }

        question.Progress.Algorithm = scheduler.Algorithm;
        await _repo.AddReviewAsync(review);

        return question.Progress;
    }

    public async Task ReRateAttemptAnswerAsync(Guid attemptId, Guid questionId, Guid userId, ReviewRating rating)
    {
        // Not scheduled yet (finished before repetition existed): the backfill will use the new rating
        var review = await _repo.GetAttemptReviewAsync(attemptId, questionId);
        if (review is null) return;

        review.Rating = rating;
        await RebuildQuestionAsync(questionId, userId);
    }

    public async Task RebuildQuestionAsync(Guid questionId, Guid userId)
    {
        var question = (await _repo.GetQuestionsWithProgressAsync([questionId], userId)).FirstOrDefault();
        if (question is null) return;

        var reviews = await _repo.GetReviewsForQuestionsAsync([questionId]);
        await RebuildAsync(question, reviews, await ForUserAsync(userId), DateTime.UtcNow);
    }

    public async Task<int> RescheduleUserAsync(Guid userId)
    {
        var ids = await _repo.GetQuestionIdsWithProgressAsync(userId);
        var scheduler = await ForUserAsync(userId);

        foreach (var chunk in ids.Chunk(BatchSize))
        {
            var questions = await _repo.GetQuestionsWithProgressAsync(chunk, userId);
            var reviews = (await _repo.GetReviewsForQuestionsAsync(chunk)).ToLookup(r => r.QuestionId);
            var now = DateTime.UtcNow;

            foreach (var question in questions)
                await RebuildAsync(question, reviews[question.Id].ToList(), scheduler, now);

            await _repo.SaveChangesAsync();
        }

        return ids.Count;
    }

    public async Task<int> BackfillAsync()
    {
        var added = 0;
        var touched = new Dictionary<Guid, Guid>();

        // Saved reviews leave the "unreviewed" filter, so every batch is the next one
        while (true)
        {
            var results = await _repo.GetUnreviewedFinishedResultsAsync(BatchSize);
            if (results.Count == 0) break;

            foreach (var result in results)
            {
                await _repo.AddReviewAsync(ToReview(result, result.AttemptId));
                touched[result.QuestionId] = result.UserId;
            }

            await _repo.SaveChangesAsync();
            added += results.Count;
        }

        // Replaying the full log fills Prev* snapshots and progress in true time order
        foreach (var chunk in touched.Chunk(BatchSize))
        {
            var byUser = chunk.GroupBy(x => x.Value);
            var now = DateTime.UtcNow;

            foreach (var group in byUser)
            {
                var ids = group.Select(x => x.Key).ToList();
                var questions = await _repo.GetQuestionsWithProgressAsync(ids, group.Key);
                var reviews = (await _repo.GetReviewsForQuestionsAsync(ids)).ToLookup(r => r.QuestionId);
                var scheduler = await ForUserAsync(group.Key);

                foreach (var question in questions)
                    await RebuildAsync(question, reviews[question.Id].ToList(), scheduler, now);
            }

            await _repo.SaveChangesAsync();
        }

        return added;
    }

    public async Task<int> RebuildOutdatedAsync()
    {
        var scheduler = _schedulers.Current;
        var processed = 0;
        var seen = new HashSet<Guid>();

        // Same approach as flashcards: rebuilt questions leave the filter, a repeat means nothing changed
        while (true)
        {
            var questions = await _repo.GetQuestionsWithOutdatedProgressAsync(scheduler.Algorithm, BatchSize);
            if (questions.Count == 0 || questions.All(x => !seen.Add(x.Id))) break;

            var reviews = (await _repo.GetReviewsForQuestionsAsync(questions.Select(x => x.Id).ToList()))
                .ToLookup(r => r.QuestionId);
            var now = DateTime.UtcNow;

            foreach (var question in questions)
                await RebuildAsync(question, reviews[question.Id].ToList(), await ForUserAsync(question.UserId), now);

            await _repo.SaveChangesAsync();
            processed += questions.Count;
        }

        return processed;
    }

    private async Task RebuildAsync(
        Question question,
        List<QuestionReview> reviews,
        IFlashcardScheduler scheduler,
        DateTime now)
    {
        var card = FlashcardProgressRebuilder.Replay(reviews, scheduler);

        if (card is null)
        {
            if (question.Progress is not null)
                _repo.RemoveProgress(question.Progress);

            question.Progress = null;
            return;
        }

        if (question.Progress is null)
        {
            question.Progress = QuestionReviewMapper.ToProgressEntity(question.Id, reviews[^1].UserId, card, now);
            question.Progress.Algorithm = scheduler.Algorithm;
            await _repo.AddProgressAsync(question.Progress);
            return;
        }

        FlashcardMapper.ApplyCard(question.Progress, card, now);
        question.Progress.Algorithm = scheduler.Algorithm;
    }

    // The configured algorithm with the user's own retention, if they set one
    private async Task<IFlashcardScheduler> ForUserAsync(Guid userId) =>
        _learning is null
            ? _schedulers.Current
            : UserFlashcardScheduler.For(_schedulers.Current, (await _learning.GetAsync(userId)).Overrides);

    private static DateTime AnsweredAt(Result result) => result.LastModifiedAt ?? result.AnsweredAt;

    private static QuestionReview ToReview(Result result, Guid attemptId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = result.UserId,
        QuestionId = result.QuestionId,
        AttemptId = attemptId,
        ResultId = result.Id,
        Rating = RatingOf(result),
        ReviewedAt = AnsweredAt(result),
    };

    // Answers saved before ratings existed have none: correctness alone decides
    internal static ReviewRating RatingOf(Result result) =>
        result.Rating != ReviewRating.Manual
            ? result.Rating
            : result.IsCorrect ? ReviewRating.Good : ReviewRating.Again;
}
