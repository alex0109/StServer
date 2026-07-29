using Application.DTOs.Statistics;
using Application.Interfaces;
using Domain.Utility.Result;

namespace Application.Services;

public class StatisticsService : IStatisticsService
{
    private readonly IStatisticsRepository _repo;
    private readonly IUserContext _user;

    public StatisticsService(IStatisticsRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }

    public async Task<StatisticsOverviewDto> GetOverviewAsync()
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

        // average score per attempt, then averaged across attempts —
        // this weighs every attempt equally, regardless of how many questions it had
        var attemptScores = attempts
            .Select(a => a.Results.Count == 0 ? 0 : a.Results.Average(r => r.Score))
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

    public async Task<List<AttemptTrendPointDto>> GetAttemptTrendAsync(int days = 30)
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
                AverageScore = Math.Round(
                    g.Average(a => a.Results.Count == 0 ? 0 : a.Results.Average(r => r.Score)),
                    2)
            })
            .ToList();
    }

    public async Task<List<DifficultyStatDto>> GetDifficultyBreakdownAsync()
    {
        var results = await _repo.GetResultsWithQuestionAndTagsAsync(_user.UserId);

        return results
            .GroupBy(r => r.Question.QuestionDifficulty)
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
    public async Task<List<ConfidenceCalibrationDto>> GetConfidenceCalibrationAsync()
    {
        var results = await _repo.GetResultsWithQuestionAndTagsAsync(_user.UserId);
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
    public async Task<List<TagPerformanceDto>> GetTagPerformanceAsync()
    {
        var results = await _repo.GetResultsWithQuestionAndTagsAsync(_user.UserId);

        var tagAnswers = results
            .SelectMany(r => r.Question.Material.MaterialTags.Select(mt => new { mt.Tag, Result = r }));

        return tagAnswers
            .GroupBy(x => x.Tag)
            .Select(g => new TagPerformanceDto
            {
                TagId = g.Key.Id,
                TagName = g.Key.Name,
                AnswersCount = g.Count(),
                Accuracy = Math.Round((double)g.Count(x => x.Result.IsCorrect) / g.Count() * 100, 1)
            })
            .OrderBy(t => t.Accuracy)
            .ToList();
    }

    public async Task<StudyStreakDto> GetStudyStreakAsync()
    {
        var attempts = await _repo.GetFinishedAttemptsWithResultsAsync(_user.UserId);

        var studyDays = attempts
            .Where(a => a.FinishedAt is not null)
            .Select(a => a.FinishedAt!.Value.Date)
            .ToHashSet();

        var today = DateTime.UtcNow.Date;
        var cursor = studyDays.Contains(today) ? today : today.AddDays(-1);

        var currentStreak = 0;
        while (studyDays.Contains(cursor))
        {
            currentStreak++;
            cursor = cursor.AddDays(-1);
        }

        var longestStreak = 0;
        var running = 0;
        DateTime? previous = null;

        foreach (var day in studyDays.OrderBy(d => d))
        {
            running = previous is not null && day == previous.Value.AddDays(1)
                ? running + 1
                : 1;

            longestStreak = Math.Max(longestStreak, running);
            previous = day;
        }

        return new StudyStreakDto
        {
            CurrentStreakDays = currentStreak,
            LongestStreakDays = longestStreak
        };
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
