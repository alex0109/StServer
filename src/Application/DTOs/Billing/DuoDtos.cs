namespace Application.DTOs.Billing;

public enum DuoRole
{
    None,
    // Pays for the Duo and invites
    Owner,
    // Joined someone's Duo with their link
    Member,
}

// Active: the payer's Duo is in force, so the member has the plan now. The other person's email
// is shown to each of them; a member sees nothing of the invite
public record DuoStatusDto(
    DuoRole Role,
    bool Active,
    bool CanInvite,
    DateTime? InviteExpiresAt,
    string? MemberEmail,
    DateTime? JoinedAt,
    string? OwnerEmail);

// The token goes into the link the client builds; it is shown this once
public record DuoInviteDto(string Token, DateTime ExpiresAt);

// What the join page shows before the user agrees
public record DuoInvitePreviewDto(string? OwnerEmail, bool IsOwn);

public class DuoTokenDto
{
    public string Token { get; set; } = "";
}
