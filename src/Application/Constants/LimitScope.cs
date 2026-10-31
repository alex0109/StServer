namespace Application.Constants;

// What a limit counts, together with the owner id it forms the lock key
public enum LimitScope
{
    Materials = 1,
    Tags = 2,
    Questions = 3,
    Notes = 4,
    Dictionaries = 5,
    DictionaryEntries = 6,
    AiUsage = 7,
    Feedback = 8,
    LibraryRequests = 9,
}
