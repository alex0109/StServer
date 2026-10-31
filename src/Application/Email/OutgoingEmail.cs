namespace Application.Email;

// Provider neutral email, the body is built by templates in Application
public record OutgoingEmail(
    string To,
    string Subject,
    string Html,
    string Text,
    string? ReplyTo = null);
