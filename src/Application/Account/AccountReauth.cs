using System.Globalization;
using System.Text.Json;

namespace Application.Account;

// Supabase tokens are refreshed for weeks, so a valid token says nothing about who sits at the screen now.
// The "amr" claim keeps the time of the sign in itself: a refresh doesn't change it
public static class AccountReauth
{
    public static readonly TimeSpan MaxSignInAge = TimeSpan.FromMinutes(10);

    // Raw names and the names .NET maps them to when it reads a token
    public static readonly string[] AmrClaimTypes = ["amr", "http://schemas.microsoft.com/claims/authnmethodsreferences"];
    public static readonly string[] AuthTimeClaimTypes = ["auth_time", "http://schemas.microsoft.com/ws/2008/06/identity/claims/authenticationinstant"];

    // Latest sign in from claim values: {"method":"otp","timestamp":1730000000}, an array of those,
    // or a plain unix time for auth_time. Null when nothing has a time
    public static DateTimeOffset? LastSignInAt(IEnumerable<string> amrValues, IEnumerable<string> authTimeValues)
    {
        long? latest = null;

        foreach (var value in amrValues.Concat(authTimeValues))
        {
            foreach (var seconds in Timestamps(value))
            {
                if (latest is null || seconds > latest) latest = seconds;
            }
        }

        return latest is null ? null : DateTimeOffset.FromUnixTimeSeconds(latest.Value);
    }

    // A minute of clock skew either way, the token comes from another server
    public static bool IsFresh(DateTimeOffset? signedInAt, DateTimeOffset now) =>
        signedInAt is { } at
        && at <= now.AddMinutes(1)
        && now - at <= MaxSignInAge;

    private static IEnumerable<long> Timestamps(string value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var plain))
        {
            return [plain];
        }

        try
        {
            using var json = JsonDocument.Parse(value);
            var found = new List<long>();
            Collect(json.RootElement, found);
            return found;
        }
        catch (JsonException)
        {
            // A bare method name like "otp" has no time
            return [];
        }
    }

    private static void Collect(JsonElement element, List<long> found)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) Collect(item, found);
                break;
            case JsonValueKind.Object when element.TryGetProperty("timestamp", out var timestamp)
                                           && timestamp.TryGetInt64(out var seconds):
                found.Add(seconds);
                break;
            case JsonValueKind.Number when element.TryGetInt64(out var number):
                found.Add(number);
                break;
        }
    }
}
