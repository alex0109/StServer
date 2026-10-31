namespace Application.DTOs.Account;

public class AccountDeleteDto
{
    // Typed by the user, must be the email of the signed in account
    public string? ConfirmEmail { get; set; }

    // Language of the confirmation email, the one the user sees the site in
    public string? Language { get; set; }
}
