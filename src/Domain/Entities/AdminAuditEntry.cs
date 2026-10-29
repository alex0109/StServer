using System.ComponentModel.DataAnnotations;
using Domain.Utility.Admin;

namespace Domain.Entities;

// One action of an admin: who, what, on which object, from where. Never edited or deleted by the app
public class AdminAuditEntry
{
    public Guid Id { get; set; }
    public required Guid AdminUserId { get; set; }
    public required AdminAction Action { get; set; }
    public Guid? TargetId { get; set; }

    [MaxLength(500)]
    public string? Details { get; set; }

    [MaxLength(64)]
    public string? Ip { get; set; }

    [MaxLength(300)]
    public string? UserAgent { get; set; }

    public required DateTime CreatedAt { get; set; }
}
