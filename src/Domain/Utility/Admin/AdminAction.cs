namespace Domain.Utility.Admin;

// What the library owner did in the admin panel, for the audit log
public enum AdminAction
{
    SignIn = 1,
    ApproveRequest = 2,
    RejectRequest = 3,
    PublishItem = 4,
    RemoveItem = 5,

    // Not an admin action: a user deleted their own account. Kept here so the owner sees erasures in one log
    AccountDeleted = 6,

    // Plans given or taken back by hand, e.g. Plus for beta testers
    GrantSubscription = 7,
    RevokeSubscription = 8,
}
