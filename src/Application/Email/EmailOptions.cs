namespace Application.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    // Sender shown in the inbox. "onboarding@resend.dev" works without an own domain,
    // but only for emails to the address of the Resend account
    public string From { get; set; } = "Studytracker <onboarding@resend.dev>";

    // Where feedback from the site goes
    public string FeedbackTo { get; set; } = "";

    // Admin sign-ins and denied admin access; empty means the feedback address
    public string AdminAlertsTo { get; set; } = "";

    // Base address of the site, for links in emails
    public string AppUrl { get; set; } = "http://localhost:3000";
}
