using Application.DTOs.Notification;

namespace Application.Interfaces;

public interface INotificationService
{
    Task<NotificationSettingsDto> GetSettingsAsync();
    Task<NotificationSettingsDto> UpdateSettingsAsync(NotificationSettingsUpdateDto dto);
    Task<NotificationFeedDto> GetFeedAsync();
    Task<bool> MarkReadAsync(Guid notificationId);
    Task MarkAllReadAsync();
    // Removes one from the feed; false when it isn't the user's
    Task<bool> DismissAsync(Guid notificationId);
    Task DismissAllAsync();
}

// Run by the hourly job
public interface INotificationGenerator
{
    Task RunAsync();
}
