using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

// One sent feedback message, without its text: only who sent it and when, for the monthly limit
public class FeedbackSubmission
{
    public Guid Id { get; set; }

    // "user:{id}" for signed in senders, "ip:{hash}" for guests: the address itself is not stored
    [MaxLength(100)]
    public required string SenderKey { get; set; }

    public required DateTime CreatedAt { get; set; }
}
