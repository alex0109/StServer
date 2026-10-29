using System.Text.Json;
using Application.DTOs.Statistics;
using Domain.Utility.Notification;

namespace Application.DTOs.Notification;

public class NotificationDto
{
    public required Guid Id { get; init; }
    public required NotificationType Type { get; init; }
    public required string PeriodKey { get; init; }
    // Numbers the client turns into text in the user's language
    public required JsonElement Data { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

// What the statistics page shows above the charts
public class NotificationFeedDto
{
    public required TodayDto Today { get; init; }
    public required List<NotificationDto> Notifications { get; init; }
    public required int UnreadCount { get; init; }
}

public class TodayDto
{
    public required string LocalDate { get; init; }
    public required int DueWords { get; init; }
    public required int NewWords { get; init; }
    public required List<DictionaryDueDto> Dictionaries { get; init; }
    public required int StreakDays { get; init; }
    public required bool StudiedToday { get; init; }
    // Evening, a streak is running and today has no study yet
    public required bool StreakAtRisk { get; init; }
}
