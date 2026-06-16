using StServer.Application.Interfaces;

namespace StServer.Infrastructure.Authentication;

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?
                .User.FindFirst("sub")?.Value;

            if (sub == null)
                throw new Exception("User is not authenticated");

            return Guid.Parse(sub);
        }
    }
}