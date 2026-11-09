namespace Domain.Utility.Subscription;

public enum SubscriptionTier
{
    Free = 0,
    Plus = 1,
    // Not sold any more; kept for plans granted by hand
    Pro = 2,
    // Plus for two people: the payer and one member they invite
    Duo = 3,
}
