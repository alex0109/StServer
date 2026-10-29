using Application.DTOs.Notification;
using Application.DTOs.Statistics;
using Application.Interfaces;
using Application.Statistics;

namespace Application.Services;

public class StudyActivityService : IStudyActivityService
{
    private const int RecentDaysCount = 14;
    private const int MaxDictionariesInReminder = 5;
    private const int EveningHour = 18;

    private readonly IStatisticsRepository _repo;

    public StudyActivityService(IStatisticsRepository repo)
    {
        _repo = repo;
    }

    public async Task<StudyStreakDto> GetStreakAsync(Guid userId, TimeZoneInfo timeZone, DateTime utcNow)
    {
        var days = StudyCalendar.ToLocalDays(await _repo.GetActivityTimesAsync(userId), timeZone);
        var today = UserTimeZone.LocalDate(utcNow, timeZone);
        var (current, longest) = StudyCalendar.Streak(days, today);

        return new StudyStreakDto
        {
            CurrentStreakDays = current,
            LongestStreakDays = longest,
            StudiedToday = days.Contains(today),
            RecentDays = StudyCalendar.RecentDays(days, today, RecentDaysCount),
        };
    }

    public async Task<TodayDto> GetTodayAsync(Guid userId, TimeZoneInfo timeZone, DateTime utcNow)
    {
        var local = UserTimeZone.ToLocal(utcNow, timeZone);
        var today = DateOnly.FromDateTime(local);

        var dictionaries = await _repo.GetDueWordsAsync(userId, UserTimeZone.EndOfLocalDayUtc(today, timeZone));
        var streak = await GetStreakAsync(userId, timeZone, utcNow);

        return new TodayDto
        {
            LocalDate = today.ToString("yyyy-MM-dd"),
            DueWords = dictionaries.Sum(x => x.Due),
            NewWords = dictionaries.Sum(x => x.New),
            Dictionaries = dictionaries.Take(MaxDictionariesInReminder).ToList(),
            StreakDays = streak.CurrentStreakDays,
            StudiedToday = streak.StudiedToday,
            StreakAtRisk = streak.CurrentStreakDays > 0 && !streak.StudiedToday && local.Hour >= EveningHour,
        };
    }

    public async Task<WeeklyDigestData> GetWeekAsync(Guid userId, TimeZoneInfo timeZone, DateOnly weekEnd, DateTime utcNow)
    {
        var weekStart = weekEnd.AddDays(-7);
        var from = UserTimeZone.StartOfLocalDayUtc(weekStart, timeZone);
        var to = UserTimeZone.StartOfLocalDayUtc(weekEnd, timeZone);

        var activity = await _repo.GetActivityTimesAsync(userId, from);
        var studyDays = StudyCalendar.ToLocalDays(activity.Where(x => x < to), timeZone).Count;

        var attempts = (await _repo.GetFinishedAttemptsWithResultsAsync(userId, from, to))
            .Where(a => a.FinishedAt < to)
            .ToList();
        var answers = attempts.SelectMany(a => a.Results).ToList();

        var tagStats = await _repo.GetTagAnswerStatsAsync(userId, from, to);

        var previousFrom = UserTimeZone.StartOfLocalDayUtc(weekStart.AddDays(-7), timeZone);
        var previousAnswers = (await _repo.GetFinishedAttemptsWithResultsAsync(userId, previousFrom, from))
            .Where(a => a.FinishedAt < from)
            .SelectMany(a => a.Results)
            .ToList();

        // Topics with too few answers say nothing yet
        var weakest = tagStats
            .Where(t => t.Answers >= 2)
            .Select(t => new WeakTopicData(t.TagName, Percent(t.Correct, t.Answers), t.Answers))
            .Where(x => x.Accuracy < 100)
            .OrderBy(x => x.Accuracy)
            .Take(3)
            .ToList();

        var streak = await GetStreakAsync(userId, timeZone, utcNow);

        return new WeeklyDigestData(
            From: weekStart.ToString("yyyy-MM-dd"),
            To: weekEnd.AddDays(-1).ToString("yyyy-MM-dd"),
            StudyDays: studyDays,
            StreakDays: streak.CurrentStreakDays,
            Attempts: attempts.Count,
            Answers: answers.Count,
            Accuracy: answers.Count == 0 ? null : Percent(answers.Count(r => r.IsCorrect), answers.Count),
            PreviousAccuracy: previousAnswers.Count == 0
                ? null
                : Percent(previousAnswers.Count(r => r.IsCorrect), previousAnswers.Count),
            AverageScore: attempts.Count == 0
                ? null
                : Math.Round(attempts.Average(a => AttemptScore.Weighted(a.Results.ToList())), 1),
            Reviews: await _repo.GetReviewsCountAsync(userId, from, to),
            WeakestTopics: weakest);
    }

    private static double Percent(int part, int total) => Math.Round((double)part / total * 100, 1);
}
