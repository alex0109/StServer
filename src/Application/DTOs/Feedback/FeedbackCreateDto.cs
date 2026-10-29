namespace Application.DTOs.Feedback;

public class FeedbackCreateDto
{
    public string? Name { get; set; }
    // Optional, to answer the user
    public string? Email { get; set; }
    public required string Message { get; set; }
    public FeedbackCategory Category { get; set; } = FeedbackCategory.Other;
    // Hidden field: people never fill it, bots usually do
    public string? Website { get; set; }
}

public enum FeedbackCategory
{
    Idea,
    Bug,
    Praise,
    Other
}
