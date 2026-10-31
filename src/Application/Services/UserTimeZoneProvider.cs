using Application.Interfaces;
using Application.Statistics;

namespace Application.Services;

public class UserTimeZoneProvider : IUserTimeZoneProvider
{
    private readonly IUserProfileRepository _profiles;

    public UserTimeZoneProvider(IUserProfileRepository profiles)
    {
        _profiles = profiles;
    }

    // From the profile; UTC until the browser sent a time zone
    public async Task<TimeZoneInfo> GetAsync(Guid userId) =>
        UserTimeZone.Resolve(await _profiles.GetTimeZoneAsync(userId));
}
