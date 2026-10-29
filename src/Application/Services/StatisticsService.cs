using Application.DTOs.Statistics;
using Application.Interfaces;
using Application.Statistics;
using Domain.Utility.Result;

namespace Application.Services;

public class StatisticsService : IStatisticsService
{
    private readonly IStatisticsRepository _repo;
    private readonly IUserContext _user;
    private readonly IStudyActivityService _activity;
    private readonly INotificationRepository _notificationRepo;
    private readonly TimeProvider _time;
    private readonly IUserReadCache _cache;

    public StatisticsService(
        IStatisticsRepository repo,
        IUserContext user,
        IStudyActivityService activity,
        INotificationRepository notificationRepo,
        TimeProvider time,
        IUserReadCache cache)
    {
        _repo = repo;
        _user = user;
        _activity = activity;
        _notificationRepo = notificationRepo;
        _time = time;
        _cache = cache;
    }

    // Every statistic is cached per user until they change something (or for a few minutes)
    public Task<StatisticsOverviewDto> GetOverviewAsync() =>
        _cache.GetOrCreateAsync(_user.UserId, "statistics:overview", BuildOverviewAsync);

    public Task<List<AttemptTrendPointDto>> GetAttemptTrendAsync(int days = 30)
    {
        // A sane window also keeps the number of cached variants small
        days = Math.Clamp(days, 1, 365);

        return _cache.GetOrCreateAsync(_user.UserId, $"statistics:trend:{days}", () => BuildAttemptTrendAsync(days));
    }

    public Task<List<DifficultyStatDto>> GetDifficultyBreakdownAsync() =>
        _cache.GetOrCreateAsync(_user.UserId, "statistics:difficulty", BuildDifficultyBreakdownAsync);

    public Task<List<ConfidenceCalibrationDto>> GetConfidenceCalibrationAsync() =>
        _cache.GetOrCreateAsync(_user.UserId, "statistics:confidence", BuildConfidenceCalibrationAsync);

    public Task<List<TagPerformanceDto>> GetTagPerformanceAsync() =>
        _cache.GetOrCreateAsync(_user.UserId, "statistics:tags", BuildTagPerformanceAsync);

    // "Studied today" turns over at local midnight, so the streak is kept for a minute only
    public Task<StudyStreakDto> GetStudyStreakAsync() =>
        _cache.GetOrCreateAsync(_user.UserId, "statistics:streak", BuildStudyStreakAsync, TimeSpan.FromMinutes(1));

    private async Task<StatisticsOverviewDto> BuildOverviewAsync()
    {
        var materialsCount = await _repo.GetMaterialsCountAsync(_user.UserId);
        var questionsCount = await _repo.GetQuestionsCountAsync(_user.UserId);
        var attempts = await _repo.GetFinishedAttemptsWithResultsAsync(_user.UserId);

        var allResults = attempts.SelectMany(a => a.Results).ToList();
        var totalAnswers = allResults.Count;
        var correctAnswers = allResults.Count(r => r.IsCorrect);

        var overallAccuracy = totalAnswers == 0
            ? 0
            : (double)correctAnswers / totalAnswers * 100;

        // Score of every attempt as the results page shows it, then averaged across attempts —
        // this weighs every attempt equally, regardless of how many questions it had
        var attemptScores = attempts
            .Select(a => AttemptScore.Weighted(a.Results.ToList()))
            .ToList();

        var averageScorePerAttempt = attemptScores.Count == 0 ? 0 : attemptScores.Average();
        var consistencyScore = CalculateStandardDeviation(attemptScores);

        var totalTimeSpent = TimeSpan.FromTicks(allResults.Sum(r => r.TimeSpent.Ticks));
        var averageTimePerQuestion = totalAnswers == 0
            ? TimeSpan.Zero
            : TimeSpan.FromTicks(totalTimeSpent.Ticks / totalAnswers);

        return new StatisticsOverviewDto
        {
            TotalMaterials = materialsCount,
            TotalQuestions = questionsCount,
            TotalFinishedAttempts = attempts.Count,
            OverallAccuracy = Math.Round(overallAccuracy, 1),
            AverageScorePerAttempt = Math.Round(averageScorePerAttempt, 2),
            ConsistencyScore = Math.Round(consistencyScore, 2),
            TotalTimeSpent = totalTimeSpent,
            AverageTimePerQuestion = averageTimePerQuestion
        };
    }

    private async Task<List<AttemptTrendPointDto>> BuildAttemptTrendAsync(int days)
    {
        var from = DateTime.UtcNow.Date.AddDays(-days);
        var attempts = await _repo.GetFinishedAttemptsWithResultsAsync(_user.UserId, from);

        return attempts
            .Where(a => a.FinishedAt is not null)
            .GroupBy(a => a.FinishedAt!.Value.Date)
            .OrderBy(g => g.Key)
            .Select(g => new AttemptTrendPointDto
            {
                Date = g.Key,
                AttemptsCount = g.Count(),
                AverageScore = Math.Round(g.Average(a => AttemptScore.Weighted(a.Results.ToList())), 2)
            })
            .ToList();
    }

    private async Task<List<DifficultyStatDto>> BuildDifficultyBreakdownAsync()
    {
        var results = await _repo.GetAnswerStatRowsAsync(_user.UserId);

        return results
            .GroupBy(r => r.Difficulty)
            .OrderBy(g => g.Key)
            .Select(g => new DifficultyStatDto
            {
                Difficulty = g.Key,
                AnswersCount = g.Count(),
                Accuracy = Math.Round((double)g.Count(r => r.IsCorrect) / g.Count() * 100, 1),
                AverageTimeSpent = TimeSpan.FromTicks((long)g.Average(r => r.TimeSpent.Ticks))
            })
            .ToList();
    }

    // Compares how confident the user said they were vs how often they were
    // actually right. A big negative CalibrationGap on "High" confidence
    // answers means the user is overconfident — a genuinely useful signal
    // that raw counts alone would never surface.
    private async Task<List<ConfidenceCalibrationDto>> BuildConfidenceCalibrationAsync()
    {
        var results = await _repo.GetAnswerStatRowsAsync(_user.UserId);
        var withConfidence = results.Where(r => r.ConfidenceLevel is not null).ToList();

        return withConfidence
            .GroupBy(r => r.ConfidenceLevel!.Value)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var accuracy = (double)g.Count(r => r.IsCorrect) / g.Count();
                var expectedAccuracy = g.Key switch
                {
                    ConfidenceLevel.Low => 0.33,
                    ConfidenceLevel.Medium => 0.66,
                    ConfidenceLevel.High => 1.0,
                    _ => 0.5
                };

                return new ConfidenceCalibrationDto
                {
                    ConfidenceLevel = g.Key,
                    AnswersCount = g.Count(),
                    ActualAccuracy = Math.Round(accuracy * 100, 1),
                    CalibrationGap = Math.Round((accuracy - expectedAccuracy) * 100, 1)
                };
            })
            .ToList();
    }

    // Sorted ascending by accuracy, so index 0 is the user's weakest topic —
    // ready to plug straight into a "focus on this" UI card.
    private async Task<List<TagPerformanceDto>> BuildTagPerformanceAsync()
    {
        var tags = await _repo.GetTagAnswerStatsAsync(_user.UserId);

        return tags
            .Select(t => new TagPerformanceDto
            {
                TagId = t.TagId,
                TagName = t.TagName,
                AnswersCount = t.Answers,
                Accuracy = Math.Round((double)t.Correct / t.Answers * 100, 1)
            })
            .OrderBy(t => t.Accuracy)
            .ToList();
    }

    // Days are counted in the user's time zone; a finished attempt or a flashcard review makes a day
    private async Task<StudyStreakDto> BuildStudyStreakAsync()
    {
        var settings = await _notificationRepo.GetSettingsAsync(_user.UserId);

        return await _activity.GetStreakAsync(
            _user.UserId,
            UserTimeZone.Resolve(settings?.TimeZone),
            _time.GetUtcNow().UtcDateTime);
    }

    // Sample standard deviation of per-attempt scores.
    // Low value = performance is stable session to session.
    // High value = results swing a lot — inconsistent prep, guessing, or fatigue.
    private static double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count < 2) return 0;

        var average = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));

        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }
}
