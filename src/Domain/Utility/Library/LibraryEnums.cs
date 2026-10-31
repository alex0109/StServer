namespace Domain.Utility.Library;

public enum LibraryItemStatus
{
    Published = 1,
    // Taken out of the library: hidden, copies already imported stay with their owners
    Removed = 2,
}

// The marker of a request: what the author asks the library owner to do
public enum LibraryRequestType
{
    Publish = 1,
    Update = 2,
    Remove = 3,
}

public enum LibraryRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

// Why a request was declined: the author sees it in their language
public enum LibraryRejectionReason
{
    // Not suitable content: off-topic, low quality, copied, unsafe
    Content = 1,
    Title = 2,
    Nickname = 3,
    Other = 4,
}
