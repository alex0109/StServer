using System.ComponentModel.DataAnnotations;
using Domain.Utility.Subscription;

namespace Domain.Entities;

// Users without a row are on the Free tier. The tier in force is worked out from status and dates
// in one place (SubscriptionRules), limits and AI quotas read it from there
public class UserSubscription
{
    public Guid UserId { get; set; }
    public SubscriptionTier Tier { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public SubscriptionProvider Provider { get; set; } = SubscriptionProvider.Manual;

    [MaxLength(100)]
    public string? ProviderCustomerId { get; set; }
    [MaxLength(100)]
    public string? ProviderSubscriptionId { get; set; }

    public DateTime? CurrentPeriodStart { get; set; }
    // Null means it does not expire; an expired subscription falls back to Free
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public bool CancelAtPeriodEnd { get; set; }

    // Hand-made grants, e.g. Plus for beta testers
    public Guid? GrantedByAdminId { get; set; }
    [MaxLength(300)]
    public string? GrantReason { get; set; }

    // Time of the provider event the row reflects: older events that arrive late are skipped
    public DateTime? LastEventAt { get; set; }

    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
