namespace Application.Statistics;

// Days with study activity in the user's own calendar
public static class StudyCalendar
{
    public static HashSet<DateOnly> ToLocalDays(IEnumerable<DateTime> utcTimes, TimeZoneInfo timeZone) =>
        utcTimes.Select(x => UserTimeZone.LocalDate(x, timeZone)).ToHashSet();

    // A streak still counts today if the user studied yesterday: the day is not over yet
    public static (int Current, int Longest) Streak(IReadOnlySet<DateOnly> days, DateOnly today)
    {
        var cursor = days.Contains(today) ? today : today.AddDays(-1);

        var current = 0;
        while (days.Contains(cursor))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        var longest = 0;
        var running = 0;
        DateOnly? previous = null;

        foreach (var day in days.Order())
        {
            running = previous is not null && day == previous.Value.AddDays(1) ? running + 1 : 1;
            longest = Math.Max(longest, running);
            previous = day;
        }

        return (current, longest);
    }

    // Oldest first, the last item is today
    public static List<bool> RecentDays(IReadOnlySet<DateOnly> days, DateOnly today, int count) =>
        Enumerable.Range(0, count)
            .Select(i => days.Contains(today.AddDays(i - count + 1)))
            .ToList();
}
