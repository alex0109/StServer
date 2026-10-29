using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Notification;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;

    public NotificationRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UserNotificationSettings?> GetSettingsAsync(Guid userId)
    {
        return await _db.UserNotificationSettings.FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task AddSettingsAsync(UserNotificationSettings settings)
    {
        await _db.UserNotificationSettings.AddAsync(settings);
    }

    public async Task<List<UserNotificationSettings>> GetSettingsPageAsync(int skip, int take)
    {
        return await _db.UserNotificationSettings
            .AsNoTracking()
            .OrderBy(x => x.UserId)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(Guid userId, NotificationType type, string periodKey)
    {
        return await _db.UserNotifications
            .AnyAsync(x => x.UserId == userId && x.Type == type && x.PeriodKey == periodKey);
    }

    public async Task<bool> TryAddAsync(UserNotification notification)
    {
        await _db.UserNotifications.AddAsync(notification);

        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _db.Entry(notification).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<List<UserNotification>> GetRecentAsync(Guid userId, int take)
    {
        return await _db.UserNotifications
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> CountUnreadAsync(Guid userId)
    {
        return await _db.UserNotifications.CountAsync(x => x.UserId == userId && x.ReadAt == null);
    }

    public async Task<UserNotification?> GetAsync(Guid notificationId, Guid userId)
    {
        return await _db.UserNotifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
    }

    public async Task MarkAllReadAsync(Guid userId, DateTime readAt)
    {
        await _db.UserNotifications
            .Where(x => x.UserId == userId && x.ReadAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(n => n.ReadAt, readAt));
    }

    public async Task<int> DeleteCreatedBeforeAsync(DateTime createdBefore)
    {
        return await _db.UserNotifications
            .Where(x => x.CreatedAt < createdBefore)
            .ExecuteDeleteAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
