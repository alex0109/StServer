using Application.Interfaces;
using Application.Library;
using Application.Schedulers;
using Application.Schedulers.Fsrs;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Attempt;
using Domain.Utility.Flashcard;
using Domain.Utility.Question;
using Domain.Utility.Review;
using Shouldly;
using UnitTests.Library;

namespace UnitTests.Services;

public class QuestionReviewServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _start = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private readonly FakeRepo _repo = new();
    private readonly QuestionReviewService _sut;

    public QuestionReviewServiceTests()
    {
        var options = new FlashcardSchedulerOptions
        {
            Algorithm = FlashcardAlgorithm.Fsrs,
            Fsrs = new FsrsOptions { EnableFuzzing = false },
        };
        var provider = new FlashcardSchedulerProvider(
            [new BasicFlashcardScheduler(), new FsrsFlashcardScheduler(options)],
            options);

        _sut = new QuestionReviewService(_repo, provider);
    }

    private Question AddQuestion()
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MaterialId = Guid.NewGuid(),
            Title = "Which word introduces a contrast?",
            QuestionType = QuestionType.Open,
            CreatedAt = _start,
        };
        _repo.Questions.Add(question);
        return question;
    }

    private Attempt FinishedAttempt(DateTime at, params (Question Question, bool Correct, ReviewRating Rating)[] answers)
    {
        var attempt = new Attempt
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            AttemptStatus = AttemptStatus.Finished,
            StartedAt = at,
            FinishedAt = at.AddMinutes(10),
        };

        foreach (var (question, correct, rating) in answers)
        {
            attempt.Results.Add(new Result
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                AttemptId = attempt.Id,
                Attempt = attempt,
                QuestionId = question.Id,
                IsCorrect = correct,
                Score = correct ? 1 : 0,
                Weight = 1,
                Rating = rating,
                AnsweredAt = at.AddMinutes(1),
            });
        }

        _repo.Attempts.Add(attempt);
        return attempt;
    }

    [Fact]
    public async Task WrongAnswer_ComesBackSoon()
    {
        var question = AddQuestion();
        var attempt = FinishedAttempt(_start, (question, false, ReviewRating.Again));

        await _sut.ScheduleFinishedAttemptAsync(attempt);

        var progress = question.Progress.ShouldNotBeNull();
        progress.State.ShouldBe(FlashcardState.Learning);
        progress.Due.ShouldBeLessThan(_start.AddHours(1));
        _repo.Reviews.Single().Rating.ShouldBe(ReviewRating.Again);
    }

    [Fact]
    public async Task OneReviewPerQuestion_WithTheFinalRating()
    {
        // An answer changed before finishing is one Result with the last rating
        var question = AddQuestion();
        var attempt = FinishedAttempt(_start, (question, true, ReviewRating.Easy));

        await _sut.ScheduleFinishedAttemptAsync(attempt);

        var review = _repo.Reviews.ShouldHaveSingleItem();
        review.Rating.ShouldBe(ReviewRating.Easy);
        review.AttemptId.ShouldBe(attempt.Id);
        question.Progress!.State.ShouldBe(FlashcardState.Review);
    }

    [Fact]
    public async Task FinishingTwice_AddsNoSecondReview()
    {
        var question = AddQuestion();
        var attempt = FinishedAttempt(_start, (question, true, ReviewRating.Good));

        await _sut.ScheduleFinishedAttemptAsync(attempt);
        await _sut.ScheduleFinishedAttemptAsync(attempt);

        _repo.Reviews.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Backfill_TwiceGivesTheSameState()
    {
        var question = AddQuestion();
        FinishedAttempt(_start, (question, false, ReviewRating.Again));
        FinishedAttempt(_start.AddDays(1), (question, true, ReviewRating.Good));

        var added = await _sut.BackfillAsync();
        var due = question.Progress!.Due;
        var reps = question.Progress.Reps;

        var addedAgain = await _sut.BackfillAsync();

        added.ShouldBe(2);
        addedAgain.ShouldBe(0);
        _repo.Reviews.Count.ShouldBe(2);
        question.Progress.Due.ShouldBe(due);
        question.Progress.Reps.ShouldBe(reps);
        question.Progress.Lapses.ShouldBe(0);
    }

    // Old data: two results for one question in one attempt. One review, from the last answer;
    // two inserts used to break the unique index and stop the whole backfill
    [Fact]
    public async Task Backfill_SeveralResultsForOneQuestion_GiveOneReview()
    {
        var question = AddQuestion();
        var attempt = FinishedAttempt(_start, (question, false, ReviewRating.Again), (question, true, ReviewRating.Good));
        attempt.Results.Last().AnsweredAt = _start.AddMinutes(5);

        (await _sut.BackfillAsync()).ShouldBe(1);

        var review = _repo.Reviews.ShouldHaveSingleItem();
        review.Rating.ShouldBe(ReviewRating.Good);
        review.ResultId.ShouldBe(attempt.Results.Last().Id);
        (await _sut.BackfillAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Finishing_WithSeveralResultsForOneQuestion_AddsOneReview()
    {
        var question = AddQuestion();
        var attempt = FinishedAttempt(_start, (question, true, ReviewRating.Easy), (question, false, ReviewRating.Again));
        attempt.Results.Last().AnsweredAt = _start.AddMinutes(5);

        await _sut.ScheduleFinishedAttemptAsync(attempt);

        _repo.Reviews.ShouldHaveSingleItem().Rating.ShouldBe(ReviewRating.Again);
    }

    [Fact]
    public async Task Backfill_RatesOldAnswersByCorrectness()
    {
        // Answers saved before ratings existed have Manual (0)
        var question = AddQuestion();
        FinishedAttempt(_start, (question, false, ReviewRating.Manual));

        await _sut.BackfillAsync();

        _repo.Reviews.Single().Rating.ShouldBe(ReviewRating.Again);
    }

    [Fact]
    public async Task UnfinishedAttempts_AreNotBackfilled()
    {
        var question = AddQuestion();
        var attempt = FinishedAttempt(_start, (question, true, ReviewRating.Good));
        attempt.AttemptStatus = AttemptStatus.Abandoned;

        (await _sut.BackfillAsync()).ShouldBe(0);
        question.Progress.ShouldBeNull();
    }

    [Fact]
    public void LibrarySnapshot_HasNoProgress()
    {
        var material = LibraryTestData.Material(_userId);
        foreach (var question in material.Questions)
        {
            question.Progress = new QuestionProgress
            {
                QuestionId = question.Id,
                UserId = _userId,
                Due = _start,
                Reps = 7,
                CreatedAt = _start,
            };
        }

        var json = LibrarySnapshotJson.ToDocument(LibrarySnapshotMapper.FromMaterial(material)).RootElement.GetRawText();

        json.ShouldNotContain("\"reps\"", Case.Insensitive);
        json.ShouldNotContain("\"progress\"", Case.Insensitive);
    }

    private sealed class FakeRepo : IQuestionReviewRepository
    {
        public List<Question> Questions { get; } = [];
        public List<Attempt> Attempts { get; } = [];
        public List<QuestionReview> Reviews { get; } = [];

        public Task<List<Question>> GetQuestionsWithProgressAsync(IReadOnlyCollection<Guid> questionIds, Guid userId) =>
            Task.FromResult(Questions.Where(q => questionIds.Contains(q.Id) && q.UserId == userId).ToList());

        public Task<HashSet<Guid>> GetReviewedQuestionIdsAsync(Guid attemptId) =>
            Task.FromResult(Reviews.Where(r => r.AttemptId == attemptId).Select(r => r.QuestionId).ToHashSet());

        public Task<List<QuestionReview>> GetReviewsForQuestionsAsync(IReadOnlyCollection<Guid> questionIds) =>
            Task.FromResult(Reviews.Where(r => questionIds.Contains(r.QuestionId))
                .OrderBy(r => r.ReviewedAt).ThenBy(r => r.Id).ToList());

        public Task<QuestionReview?> GetAttemptReviewAsync(Guid attemptId, Guid questionId) =>
            Task.FromResult(Reviews.FirstOrDefault(r => r.AttemptId == attemptId && r.QuestionId == questionId));

        public Task<List<Result>> GetUnreviewedFinishedResultsAsync(int take) =>
            Task.FromResult(Attempts
                .Where(a => a.AttemptStatus == AttemptStatus.Finished)
                .SelectMany(a => a.Results)
                .Where(r => !Reviews.Any(q => q.AttemptId == r.AttemptId && q.QuestionId == r.QuestionId))
                .OrderBy(r => r.LastModifiedAt ?? r.AnsweredAt)
                .Take(take)
                .ToList());

        public Task<List<Question>> GetQuestionsWithOutdatedProgressAsync(FlashcardAlgorithm currentAlgorithm, int take) =>
            Task.FromResult(Questions.Where(q => q.Progress is not null && q.Progress.Algorithm != currentAlgorithm)
                .Take(take).ToList());

        public Task AddReviewAsync(QuestionReview review)
        {
            Reviews.Add(review);
            return Task.CompletedTask;
        }

        public Task AddProgressAsync(QuestionProgress progress) => Task.CompletedTask;

        public void RemoveProgress(QuestionProgress progress)
        {
            var question = Questions.FirstOrDefault(q => q.Id == progress.QuestionId);
            if (question is not null) question.Progress = null;
        }

        public Task<List<Guid>> GetQuestionIdsWithProgressAsync(Guid userId) =>
            Task.FromResult(Questions.Where(q => q.UserId == userId && q.Progress is not null).Select(q => q.Id).ToList());

        public Task SaveChangesAsync() => Task.CompletedTask;
    }
}
