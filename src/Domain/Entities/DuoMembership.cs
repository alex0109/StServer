using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

// The second person on a Duo plan. One row per payer (UserId), who invites with a link; the member
// has the plan while the payer's own subscription is a Duo in force. The row outlives a lapsed plan,
// so renewing the Duo brings the member back without a new invite
public class DuoMembership
{
    public Guid UserId { get; set; }

    public Guid? MemberUserId { get; set; }

    // Shown to the other person, so each knows who shares the plan
    [MaxLength(320)]
    public string? OwnerEmail { get; set; }
    [MaxLength(320)]
    public string? MemberEmail { get; set; }

    // SHA-256 of the open invite link's token; the token itself is shown once and never stored
    [MaxLength(64)]
    public string? InviteTokenHash { get; set; }
    public DateTime? InviteExpiresAt { get; set; }

    public DateTime? JoinedAt { get; set; }

    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
