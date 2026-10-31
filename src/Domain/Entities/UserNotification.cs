using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Domain.Utility.Notification;

namespace Domain.Entities;

// A reminder or digest made by a background job. The texts are built on the client from
// Type and Data, so they follow the language the user reads in
public class UserNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    // Day ("2026-10-25") or week ("2026-W43") it belongs to; with the type it is unique per user,
    // which is the "already sent" mark
    [MaxLength(16)]
    public required string PeriodKey { get; set; }
    public required JsonDocument Data { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? EmailSentAt { get; set; }
}
