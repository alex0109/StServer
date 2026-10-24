using Domain.Utility.Subscription;

namespace Domain.Entities;

// Users without a row are on the Free tier
public class UserSubscription
{
    public Guid UserId { get; set; }
    public SubscriptionTier Tier { get; set; }
    // Null means it does not expire; an expired subscription falls back to Free
    public DateTime? CurrentPeriodEnd { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
