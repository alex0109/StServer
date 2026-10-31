using Application.Interfaces;
using Application.Notifications;
using Application.Profile;

namespace Application.Services;

// Read once per request: a session start or a rebuild asks for the same user many times
public class LearningSettingsProvider : ILearningSettingsProvider
{
    private readonly IUserProfileRepository _profiles;
    private readonly IStatisticsRepository _statistics;
    private readonly NotificationOptions _notifications;
    private readonly Dictionary<Guid, LearningSettings> _cache = [];

    public LearningSettingsProvider(
        IUserProfileRepository profiles,
        IStatisticsRepository statistics,
        NotificationOptions notifications)
    {
        _profiles = profiles;
        _statistics = statistics;
        _notifications = notifications;
    }

    public async Task<LearningSettings> GetAsync(Guid userId)
    {
        if (_cache.TryGetValue(userId, out var cached)) return cached;

        var profile = await _profiles.GetAsync(userId);
        var settings = new LearningSettings(
            profile?.DesiredRetention,
            profile?.NewCardsPerDay,
            profile?.MaxReviewsPerDay,
            profile?.DailyReminderHour ?? _notifications.DailyReminderHour,
            profile?.StreakReminderHour ?? _notifications.StreakReminderHour,
            profile?.DailyGoal,
            profile?.StreakFreezesPerMonth ?? LearningRules.DefaultStreakFreezesPerMonth);

        _cache[userId] = settings;
        return settings;
    }

    public async Task<DailyBudget> GetBudgetAsync(Guid userId, DateTime dayStartUtc, DateTime dayEndUtc)
    {
        var settings = await GetAsync(userId);
        var (reviews, newWords) = await _statistics.CountReviewsAsync(userId, dayStartUtc, dayEndUtc);

        return new DailyBudget(
            reviews,
            newWords,
            settings.NewCardsPerDay is int newLimit ? Math.Max(0, newLimit - newWords) : null,
            settings.MaxReviewsPerDay is int reviewLimit ? Math.Max(0, reviewLimit - reviews) : null);
    }
}
