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

    public sealed record FrozenStreak(int Current, int Longest, IReadOnlySet<DateOnly> Frozen, int FreezesLeftThisMonth);

    // Missed days inside the current streak are covered by freezes, a limited number per calendar
    // month. Walking back from today every missed day takes one freeze of its month; the streak ends
    // at the first missed day its month has no freeze left for. Covered days keep the streak alive
    // but are not counted as study days. The result is computed from the history every time, so a
    // missed day always takes exactly one freeze, never two
    public static FrozenStreak Streak(IReadOnlySet<DateOnly> days, DateOnly today, int freezesPerMonth)
    {
        var (plainCurrent, longest) = Streak(days, today);
        if (freezesPerMonth <= 0 || days.Count == 0)
            return new FrozenStreak(plainCurrent, longest, new HashSet<DateOnly>(), Math.Max(0, freezesPerMonth));

        var frozen = new HashSet<DateOnly>();
        var pending = new List<DateOnly>();
        var spent = new Dictionary<(int Year, int Month), int>();
        var first = days.Min();
        var cursor = days.Contains(today) ? today : today.AddDays(-1);
        var current = 0;

        while (cursor >= first)
        {
            if (days.Contains(cursor))
            {
                current++;
                // Missed days count as covered only once a study day before them continues the streak
                frozen.UnionWith(pending);
                pending.Clear();
            }
            else
            {
                var month = (cursor.Year, cursor.Month);
                var used = spent.GetValueOrDefault(month);
                if (used >= freezesPerMonth) break;

                spent[month] = used + 1;
                pending.Add(cursor);
            }

            cursor = cursor.AddDays(-1);
        }

        var usedThisMonth = frozen.Count(d => d.Year == today.Year && d.Month == today.Month);

        return new FrozenStreak(current, Math.Max(longest, current), frozen, Math.Max(0, freezesPerMonth - usedThisMonth));
    }

    // Days where the number of reviews reached the goal
    public static HashSet<DateOnly> DaysWithGoal(IEnumerable<DateTime> utcTimes, TimeZoneInfo timeZone, int goal) =>
        utcTimes
            .GroupBy(x => UserTimeZone.LocalDate(x, timeZone))
            .Where(g => g.Count() >= goal)
            .Select(g => g.Key)
            .ToHashSet();

    // Oldest first, the last item is today
    public static List<bool> RecentDays(IReadOnlySet<DateOnly> days, DateOnly today, int count) =>
        Enumerable.Range(0, count)
            .Select(i => days.Contains(today.AddDays(i - count + 1)))
            .ToList();
}
