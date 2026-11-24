namespace Application.Interfaces;

// The user agreed, for this request, to pay with credits once the quota is used up
// (the app asks before spending them on media, and on study AI until auto-use is on)
public interface IAiCreditConsent
{
    bool UseCredits { get; }
}
