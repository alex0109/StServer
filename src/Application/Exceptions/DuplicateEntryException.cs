namespace Application.Exceptions;

// The dictionary already has this term; the client offers to update the existing word instead
public class DuplicateEntryException : ConflictException
{
    public DuplicateEntryException(Guid existingEntryId)
        : base("This word is already in the dictionary")
    {
        ExistingEntryId = existingEntryId;
    }

    public Guid ExistingEntryId { get; }
}
