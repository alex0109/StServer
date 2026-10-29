using Domain.Entities;
using Domain.Utility.Notification;

namespace Application.Interfaces;

public interface INotificationRepository
{
    Task<UserNotificationSettings?> GetSettingsAsync(Guid userId);
    Task AddSettingsAsync(UserNotificationSettings settings);
    // For the job: settings ordered by user id
    Task<List<UserNotificationSettings>> GetSettingsPageAsync(int skip, int take);

    Task<bool> ExistsAsync(Guid userId, NotificationType type, string periodKey);
    // False when the same kind for the same period already exists
    Task<bool> TryAddAsync(UserNotification notification);
    Task<List<UserNotification>> GetRecentAsync(Guid userId, int take);
    Task<int> CountUnreadAsync(Guid userId);
    Task<UserNotification?> GetAsync(Guid notificationId, Guid userId);
    Task MarkAllReadAsync(Guid userId, DateTime readAt);
    Task<int> DeleteCreatedBeforeAsync(DateTime createdBefore);
    Task SaveChangesAsync();
}
