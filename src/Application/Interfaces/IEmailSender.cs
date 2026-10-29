using Application.Email;

namespace Application.Interfaces;

// Implemented in Infrastructure, a different email service is a new implementation of this
public interface IEmailSender
{
    // False when the API key is missing, nothing is sent then
    bool IsConfigured { get; }

    // Throws EmailUnavailableException when the email could not be sent
    Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default);
}
