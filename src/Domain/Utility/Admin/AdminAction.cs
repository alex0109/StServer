namespace Domain.Utility.Admin;

// What the library owner did in the admin panel, for the audit log
public enum AdminAction
{
    SignIn = 1,
    ApproveRequest = 2,
    RejectRequest = 3,
    PublishItem = 4,
    RemoveItem = 5,
}
