using Application.DTOs.Flashcard;
using Application.DTOs.Option;
using Application.DTOs.Question;
using Application.DTOs.Result;
using Application.DTOs.Review;
using Application.Exceptions;
using Application.Interfaces;
using Application.Profile;
using Application.Review;
using Application.Statistics;
using Domain.Entities;
using Domain.Utility.Question;
using Application.Observability;

namespace Application.Services;

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _repo;
    private readonly IStatisticsRepository _statistics;
    private readonly IFlashcardService _flashcards;
    private readonly IQuestionReviewService _questionReviews;
    private readonly IAnswerEvaluationService _evaluation;
    private readonly IReviewRatingResolver _ratings;
    private readonly IUserTimeZoneProvider _timeZones;
    private readonly IUserContext _user;
    private readonly ReviewOptions _options;
    private readonly TimeProvider _time;
    private readonly ILearningSettingsProvider? _learning;

    public ReviewService(
        IReviewRepository repo,
        IStatisticsRepository statistics,
        IFlashcardService flashcards,
        IQuestionReviewService questionReviews,
        IAnswerEvaluationService evaluation,
        IReviewRatingResolver ratings,
        IUserTimeZoneProvider timeZones,
        IUserContext user,
        ReviewOptions options,
        TimeProvider time,
        ILearningSettingsProvider? learning = null)
    {
        _repo = repo;
        _statistics = statistics;
        _flashcards = flashcards;
        _questionReviews = questionReviews;
        _evaluation = evaluation;
        _ratings = ratings;
        _timeZones = timeZones;
        _user = user;
        _options = options;
        _time = time;
        _learning = learning;
    }

    public async Task<ReviewTodayDto> GetTodayAsync()
    {
        var userId = _user.UserId;
        var day = await TodayAsync(userId);

        var dictionaries = await _statistics.GetDueWordsAsync(userId, day.End);
        var materials = await _repo.GetDueQuestionsAsync(userId, day.End);

        var dueWords = dictionaries.Sum(x => x.Due);
        var newWords = dictionaries.Sum(x => x.New);
        var dueQuestions = materials.Sum(x => x.Due);
        var budget = await BudgetAsync(userId, day);

        var words = Math.Min(MaxWords(budget), dueWords + Math.Min(newWords, NewWordsLimit(budget)));
        // Words come first in the queue, questions get what the daily limit leaves
        var questions = Math.Min(MaxQuestions(budget, words), dueQuestions);
        var seconds = words * _options.SecondsPerWord + questions * _options.SecondsPerQuestion;

        return new ReviewTodayDto
        {
            LocalDate = day.Date.ToString("yyyy-MM-dd"),
            DueWords = dueWords,
            NewWords = newWords,
            DueQuestions = dueQuestions,
            OverdueWords = await _repo.CountOverdueWordsAsync(userId, day.Start),
            OverdueQuestions = await _repo.CountOverdueQuestionsAsync(userId, day.Start),
            WordsInSession = words,
            QuestionsInSession = questions,
            EstimatedMinutes = seconds == 0 ? 0 : (int)Math.Ceiling(seconds / 60.0),
            Dictionaries = dictionaries,
            Materials = materials,
            ReviewsDone = budget.ReviewsDone,
            DailyGoal = _learning is null ? null : (await _learning.GetAsync(userId)).DailyGoal,
            NewWordsLeft = budget.NewWordsLeft,
            ReviewsLeft = budget.ReviewsLeft,
        };
    }

    public async Task<FlashcardSessionResponseDto> StartWordsAsync(FlashcardSessionCreateDto dto)
    {
        var day = await TodayAsync(_user.UserId);
        var budget = await BudgetAsync(_user.UserId, day);
        if (budget.ReviewsLeft == 0)
            throw new ConflictException("The daily review limit is reached");

        return await _flashcards.StartTodaySessionAsync(dto, day.End, NewWordsLimit(budget), MaxWords(budget));
    }

    public async Task<QuestionReviewSessionDto> StartQuestionsAsync(QuestionReviewSessionCreateDto dto)
    {
        var userId = _user.UserId;
        var day = await TodayAsync(userId);
        var budget = await BudgetAsync(userId, day);
        var max = MaxQuestions(budget, 0);
        if (max == 0)
            throw new ConflictException("The daily review limit is reached");

        var limit = Math.Clamp(dto.Limit ?? max, 1, max);

        var questions = await _repo.GetDueQuestionsForSessionAsync(userId, day.End, limit);
        if (questions.Count == 0)
            throw new ConflictException("There are no questions to review");

        var session = new QuestionReviewSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            QuestionsTotal = questions.Count,
            StartedAt = _time.GetUtcNow().UtcDateTime,
        };

        await _repo.AddSessionAsync(session);
        await _repo.SaveChangesAsync();

        return new QuestionReviewSessionDto
        {
            Id = session.Id,
            StartedAt = session.StartedAt,
            Questions = questions.Select(ToReviewQuestion).ToList(),
        };
    }

    public async Task<ReviewAnswerResultDto?> AnswerQuestionAsync(Guid sessionId, ResultRequestDto dto)
    {
        var userId = _user.UserId;

        var session = await _repo.GetSessionAsync(sessionId, userId);
        if (session is null) return null;

        if (session.FinishedAt is not null)
            throw new ConflictException("Session is already finished");

        // One answer per question per session: it is a review, not a draft
        if (await _repo.IsAnsweredAsync(sessionId, dto.QuestionId))
            throw new ConflictException("The question is already answered in this session");

        var question = await _repo.GetQuestionAsync(dto.QuestionId, userId);
        if (question is null) return null;

        var correctOptionIds = question.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToList();
        var isChoice = question.QuestionType is QuestionType.Options or QuestionType.TrueFalse;

        // Same grading as attempts, including the AI check in the fuzzy gray zone
        var result = isChoice
            ? _evaluation.EvaluateOptions(correctOptionIds, dto.UserAnswerOptionIds)
            : question.Answer is not null && !string.IsNullOrWhiteSpace(dto.UserAnswer)
                ? await _evaluation.EvaluateAnswerAsync(question.Title, question.Answer, dto.UserAnswer)
                : new AnswerEvaluationResult();

        var rating = _ratings.Resolve(result.IsCorrect, dto.ConfidenceLevel);
        var progress = await _questionReviews.ReviewInSessionAsync(question, sessionId, rating);

        await _repo.SaveChangesAsync();
        AppMetrics.Review("question", "today");

        return new ReviewAnswerResultDto
        {
            QuestionId = question.Id,
            IsCorrect = result.IsCorrect,
            Score = result.Score,
            Method = result.Method,
            Rating = rating,
            CorrectAnswer = isChoice ? null : question.Answer,
            CorrectOptionIds = isChoice ? correctOptionIds : [],
            Explanation = question.Explanation,
            State = progress.State,
            NextDue = progress.Due,
        };
    }

    public async Task<bool> FinishQuestionsAsync(Guid sessionId)
    {
        var session = await _repo.GetSessionAsync(sessionId, _user.UserId);
        if (session is null) return false;

        if (session.FinishedAt is null)
        {
            session.FinishedAt = _time.GetUtcNow().UtcDateTime;
            await _repo.SaveChangesAsync();
        }

        return true;
    }

    private async Task<DailyBudget> BudgetAsync(Guid userId, LocalDay day) =>
        _learning is null ? DailyBudget.Unlimited : await _learning.GetBudgetAsync(userId, day.Start, day.End);

    private int NewWordsLimit(DailyBudget budget) =>
        Math.Min(_options.NewWordsPerSession, budget.NewWordsLeft ?? int.MaxValue);

    private int MaxWords(DailyBudget budget) =>
        Math.Min(_options.MaxWordsPerSession, budget.ReviewsLeft ?? int.MaxValue);

    private int MaxQuestions(DailyBudget budget, int wordsBefore) =>
        Math.Min(_options.MaxQuestionsPerSession, Math.Max(0, (budget.ReviewsLeft ?? int.MaxValue) - wordsBefore));

    private sealed record LocalDay(DateOnly Date, DateTime Start, DateTime End);

    // "Today" is the user's local day: a question due at 23:00 their time belongs to it
    private async Task<LocalDay> TodayAsync(Guid userId)
    {
        var timeZone = await _timeZones.GetAsync(userId);
        var today = UserTimeZone.LocalDate(_time.GetUtcNow().UtcDateTime, timeZone);

        return new LocalDay(
            today,
            UserTimeZone.StartOfLocalDayUtc(today, timeZone),
            UserTimeZone.EndOfLocalDayUtc(today, timeZone));
    }

    private static ReviewQuestionDto ToReviewQuestion(Question question) => new()
    {
        Id = question.Id,
        MaterialId = question.MaterialId,
        MaterialTitle = question.Material.Title,
        Title = question.Title,
        QuestionType = question.QuestionType,
        Options = question.Options
            .OrderBy(o => o.Position)
            .Select(o => new OptionResponseDto { Id = o.Id, Name = o.Name })
            .ToList(),
    };
}
