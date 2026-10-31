using Application.Ai;
using Application.DTOs.Profile;
using Application.Exceptions;
using Application.Interfaces;
using Application.Library;
using Application.Notifications;
using Application.Profile;
using Application.Schedulers;
using Application.Statistics;
using Domain.Entities;

namespace Application.Services;

public class UserProfileService : IUserProfileService
{
    public const int MaxDailyGoal = 1000;

    private readonly IUserProfileRepository _repo;
    private readonly INotificationRepository _settings;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;
    private readonly LearningDefaultsDto _defaults;

    public UserProfileService(
        IUserProfileRepository repo,
        INotificationRepository settings,
        IUserContext user,
        TimeProvider time,
        FlashcardSchedulerOptions? scheduler = null,
        NotificationOptions? notifications = null)
    {
        _repo = repo;
        _settings = settings;
        _user = user;
        _time = time;

        notifications ??= new NotificationOptions();
        _defaults = new LearningDefaultsDto(
            (scheduler ?? new FlashcardSchedulerOptions()).Fsrs.DesiredRetention,
            notifications.DailyReminderHour,
            notifications.StreakReminderHour,
            LearningRules.DefaultStreakFreezesPerMonth,
            LearningRules.MinRetention,
            LearningRules.MaxRetention);
    }

    public async Task<ProfileDto> GetAsync()
    {
        var userId = _user.UserId;
        var profile = await _repo.GetAsync(userId) ?? await NewFromLegacyAsync(userId);

        return ToDto(profile);
    }

    public async Task<ProfileDto> UpdateAsync(ProfileUpdateDto dto)
    {
        Validate(dto);
        // Same rule as library requests; "" clears it
        var nickname = string.IsNullOrWhiteSpace(dto.Nickname) ? null : LibraryRules.NormalizeNickname(dto.Nickname);

        var profile = await EnsureAsync(_user.UserId);

        if (dto.TimeZone is not null) profile.TimeZone = dto.TimeZone;
        if (dto.InterfaceLanguage is not null) profile.InterfaceLanguage = dto.InterfaceLanguage;
        if (dto.ExplanationLanguage is not null) profile.ExplanationLanguage = EmptyToNull(dto.ExplanationLanguage);
        if (dto.NativeLanguage is not null) profile.NativeLanguage = EmptyToNull(dto.NativeLanguage);
        if (dto.StudyLanguage is not null) profile.StudyLanguage = EmptyToNull(dto.StudyLanguage);
        if (dto.Nickname is not null) profile.Nickname = nickname;
        if (dto.DailyGoal is not null) profile.DailyGoal = dto.DailyGoal == 0 ? null : dto.DailyGoal;

        if (dto.DesiredRetention is double retention)
            profile.DesiredRetention = retention == 0 ? null : Math.Round(retention, 2);
        if (dto.NewCardsPerDay is not null) profile.NewCardsPerDay = MinusOneToNull(dto.NewCardsPerDay.Value);
        if (dto.MaxReviewsPerDay is not null) profile.MaxReviewsPerDay = MinusOneToNull(dto.MaxReviewsPerDay.Value);
        if (dto.DailyReminderHour is not null) profile.DailyReminderHour = MinusOneToNull(dto.DailyReminderHour.Value);
        if (dto.StreakReminderHour is not null) profile.StreakReminderHour = MinusOneToNull(dto.StreakReminderHour.Value);
        if (dto.StreakFreezesPerMonth is not null) profile.StreakFreezesPerMonth = MinusOneToNull(dto.StreakFreezesPerMonth.Value);

        await MirrorToLegacyAsync(profile);

        profile.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _repo.SaveChangesAsync();

        return ToDto(profile);
    }

    public async Task RememberNicknameAsync(Guid userId, string nickname)
    {
        var profile = await EnsureAsync(userId);
        if (profile.Nickname is not null) return;

        profile.Nickname = nickname;
        profile.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _repo.SaveChangesAsync();
    }

    public async Task<string> GetExplanationLanguageAsync(Guid userId)
    {
        var profile = await _repo.GetAsync(userId) ?? await NewFromLegacyAsync(userId);

        return ProfileLanguages.ResolveExplanation(profile.ExplanationLanguage, profile.InterfaceLanguage);
    }

    // Everything that reaches a prompt or a time calculation is checked before anything changes
    private static void Validate(ProfileUpdateDto dto)
    {
        if (dto.TimeZone is not null && !UserTimeZone.TryResolve(dto.TimeZone, out _))
            throw new BadRequestException("Unknown time zone");

        if (dto.InterfaceLanguage is not null && !ProfileLanguages.IsInterface(dto.InterfaceLanguage))
            throw new BadRequestException("Unknown interface language");

        if (!string.IsNullOrEmpty(dto.ExplanationLanguage) && !ProfileLanguages.IsExplanation(dto.ExplanationLanguage))
            throw new BadRequestException("Unknown explanation language");

        foreach (var name in new[] { dto.NativeLanguage, dto.StudyLanguage })
        {
            if (!string.IsNullOrWhiteSpace(name) && !AiLanguageName.IsValid(name.Trim()))
                throw new BadRequestException("Language must be a language name, e.g. \"Norwegian\"");
        }

        if (dto.DailyGoal is < 0 or > MaxDailyGoal)
            throw new BadRequestException($"Daily goal must be from 1 to {MaxDailyGoal}");

        if (dto.DesiredRetention is double retention && retention != 0
            && (double.IsNaN(retention) || retention < LearningRules.MinRetention - 0.001 || retention > LearningRules.MaxRetention + 0.001))
            throw new BadRequestException($"Retention must be from {LearningRules.MinRetention} to {LearningRules.MaxRetention}");

        CheckRange(dto.NewCardsPerDay, 0, LearningRules.MaxNewCardsPerDay, "New words a day");
        CheckRange(dto.MaxReviewsPerDay, 1, LearningRules.MaxReviewsPerDay, "Reviews a day");
        CheckRange(dto.DailyReminderHour, 0, 23, "Reminder hour");
        CheckRange(dto.StreakReminderHour, 0, 23, "Reminder hour");
        CheckRange(dto.StreakFreezesPerMonth, 0, LearningRules.MaxStreakFreezes, "Streak freezes");
    }

    // -1 means "back to the default" and is always allowed
    private static void CheckRange(int? value, int min, int max, string name)
    {
        if (value is int number && number != -1 && (number < min || number > max))
            throw new BadRequestException($"{name} must be from {min} to {max}");
    }

    private static int? MinusOneToNull(int value) => value == -1 ? null : value;

    private async Task<UserProfile> EnsureAsync(Guid userId)
    {
        var profile = await _repo.GetAsync(userId);
        if (profile is not null) return profile;

        profile = await NewFromLegacyAsync(userId);
        await _repo.AddAsync(profile);
        return profile;
    }

    // Users from before the profile keep the time zone and language their reminders already use
    private async Task<UserProfile> NewFromLegacyAsync(Guid userId)
    {
        var legacy = await _settings.GetSettingsAsync(userId);
        var now = _time.GetUtcNow().UtcDateTime;

        return new UserProfile
        {
            UserId = userId,
            TimeZone = legacy?.TimeZone,
            InterfaceLanguage = ProfileLanguages.IsInterface(legacy?.Language) ? legacy!.Language : NotificationOptions.DefaultLanguage,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private async Task MirrorToLegacyAsync(UserProfile profile)
    {
        var legacy = await _settings.GetSettingsAsync(profile.UserId);
        if (legacy is null) return;

        if (profile.TimeZone is not null) legacy.TimeZone = profile.TimeZone;
        legacy.Language = profile.InterfaceLanguage;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private ProfileDto ToDto(UserProfile profile) => new()
    {
        TimeZone = profile.TimeZone,
        InterfaceLanguage = profile.InterfaceLanguage,
        ExplanationLanguage = profile.ExplanationLanguage,
        EffectiveExplanationLanguage = ProfileLanguages.ResolveExplanation(profile.ExplanationLanguage, profile.InterfaceLanguage),
        NativeLanguage = profile.NativeLanguage,
        StudyLanguage = profile.StudyLanguage,
        Nickname = profile.Nickname,
        DailyGoal = profile.DailyGoal,
        DesiredRetention = profile.DesiredRetention,
        NewCardsPerDay = profile.NewCardsPerDay,
        MaxReviewsPerDay = profile.MaxReviewsPerDay,
        DailyReminderHour = profile.DailyReminderHour,
        StreakReminderHour = profile.StreakReminderHour,
        StreakFreezesPerMonth = profile.StreakFreezesPerMonth,
        Defaults = _defaults,
    };
}
