using Application.Notifications;
using Domain.Entities;

namespace Application.Interfaces;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetAsync(Guid userId);
    Task AddAsync(UserProfile profile);

    // The profile's time zone, or the legacy copy in the reminder settings while a profile is missing
    Task<string?> GetTimeZoneAsync(Guid userId);

    // For the reminder job: users with a time zone, ordered by id, with their email settings
    Task<List<NotificationRecipient>> GetRecipientsPageAsync(int skip, int take);

    Task SaveChangesAsync();
}
