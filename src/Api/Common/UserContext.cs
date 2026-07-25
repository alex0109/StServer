using System.Security.Claims;
using StServer.Application.Interfaces;

namespace StServer.Api.Common;

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _http;

    public UserContext(IHttpContextAccessor http)
    {
        _http = http;
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