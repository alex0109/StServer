namespace Application.Statistics;

// Time zones come from browsers as IANA names
public static class UserTimeZone
{
    // Older system time zone data knows Kyiv only by its old name
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Europe/Kyiv"] = "Europe/Kiev",
        ["Europe/Kiev"] = "Europe/Kyiv",
    };

    public static bool TryResolve(string? id, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return false;

        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var found)
            || (Aliases.TryGetValue(id, out var alias) && TimeZoneInfo.TryFindSystemTimeZoneById(alias, out found)))
        {
            timeZone = found;
            return true;
        }

        return false;
    }

    // Unknown or missing zones fall back to UTC
    public static TimeZoneInfo Resolve(string? id) => TryResolve(id, out var timeZone) ? timeZone : TimeZoneInfo.Utc;

    public static DateTime ToLocal(DateTime utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);

    public static DateOnly LocalDate(DateTime utc, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(ToLocal(utc, timeZone));

    // UTC moment when the given local day ends
    public static DateTime EndOfLocalDayUtc(DateOnly day, TimeZoneInfo timeZone)
    {
        var nextMidnight = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(nextMidnight, timeZone);
    }

    public static DateTime StartOfLocalDayUtc(DateOnly day, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), timeZone);
}
