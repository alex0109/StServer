using Application.Interfaces;

namespace Api.Common;

// The client sends this header after the user confirmed paying a request with credits
public class AiCreditConsent : IAiCreditConsent
{
    public const string Header = "X-Use-Credits";

    private readonly IHttpContextAccessor _http;

    public AiCreditConsent(IHttpContextAccessor http)
    {
        _http = http;
    }

    public bool UseCredits =>
        _http.HttpContext?.Request.Headers[Header].ToString() is { } value
        && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
