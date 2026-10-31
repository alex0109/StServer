using System.Security.Claims;
using Application.Interfaces;

namespace Api.Common;

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _http;

    public UserContext(IHttpContextAccessor http)
    {
        _http = http;
    }

    public Guid? CurrentUserId
    {
        get
        {
            var sub = _http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(sub, out var userId) ? userId : null;
        }
    }

    public string? ClientIp => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var value = _http.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, 300)];
        }
    }

    public string? Claim(string type) => _http.HttpContext?.User.FindFirst(type)?.Value;

    public IReadOnlyList<string> Claims(string type) =>
        _http.HttpContext?.User.FindAll(type).Select(x => x.Value).ToList() ?? [];

    public string? Email
    {
        get
        {
            var user = _http.HttpContext?.User;
            var email = user?.FindFirst(ClaimTypes.Email)?.Value ?? user?.FindFirst("email")?.Value;

            return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        }
    }

    public Guid UserId
    {
        get
        {
            var sub = _http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (sub is null)
                throw new Exception("Unauthenticated user");

            return Guid.Parse(sub);
        }
    }
}