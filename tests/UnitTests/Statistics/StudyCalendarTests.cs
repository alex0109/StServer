using Application.Statistics;
using Domain.Entities;
using Shouldly;

namespace UnitTests.Statistics;

public class StudyCalendarTests
{
    private static readonly DateOnly Today = new(2026, 10, 25);

    [Fact]
    public void Streak_CountsTodayAndPreviousDays()
    {
        var days = new HashSet<DateOnly> { Today, Today.AddDays(-1), Today.AddDays(-2), Today.AddDays(-5) };

        StudyCalendar.Streak(days, Today).ShouldBe((3, 3));
    }

    [Fact]
    public void Streak_NotStudiedYetToday_KeepsYesterdaysStreak()
    {
        var days = new HashSet<DateOnly> { Today.AddDays(-1), Today.AddDays(-2) };

        StudyCalendar.Streak(days, Today).Current.ShouldBe(2);
    }

    [Fact]
    public void Streak_GapBeforeYesterday_IsZero()
    {
        var days = new HashSet<DateOnly> { Today.AddDays(-2), Today.AddDays(-3), Today.AddDays(-4) };

        StudyCalendar.Streak(days, Today).ShouldBe((0, 3));
    }

    [Fact]
    public void RecentDays_LastItemIsToday()
    {
        var days = new HashSet<DateOnly> { Today, Today.AddDays(-2) };

        StudyCalendar.RecentDays(days, Today, 4).ShouldBe([false, true, false, true]);
    }

    [Fact]
    public void LocalDays_UseUserTimeZone()
    {
        // 23:30 UTC is already the next day in Kyiv
        var kyiv = UserTimeZone.Resolve("Europe/Kyiv");
        var days = StudyCalendar.ToLocalDays([new DateTime(2026, 10, 24, 23, 30, 0, DateTimeKind.Utc)], kyiv);

        days.ShouldBe([new DateOnly(2026, 10, 25)]);
    }

    [Fact]
    public void UnknownTimeZone_FallsBackToUtc()
    {
        UserTimeZone.TryResolve("Mars/Olympus", out _).ShouldBeFalse();
        UserTimeZone.Resolve("Mars/Olympus").ShouldBe(TimeZoneInfo.Utc);
    }

    [Fact]
    public void EndOfLocalDay_IsNextLocalMidnightInUtc()
    {
        var kyiv = UserTimeZone.Resolve("Europe/Kyiv");

        // Kyiv is UTC+2 in late October after the clocks went back
        UserTimeZone.EndOfLocalDayUtc(new DateOnly(2026, 10, 26), kyiv)
            .ShouldBe(new DateTime(2026, 10, 26, 22, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void AttemptScore_IsWeightedByDifficulty()
    {
        // Easy wrong (weight 1), hard right (weight 3): (0*1 + 100*3) / 4 = 75
        var results = new List<Result>
        {
            new() { IsCorrect = false, Score = 0, Weight = 1, AnsweredAt = DateTime.UtcNow },
            new() { IsCorrect = true, Score = 100, Weight = 3, AnsweredAt = DateTime.UtcNow },
        };

        AttemptScore.Weighted(results).ShouldBe(75);
        AttemptScore.Weighted([]).ShouldBe(0);
    }
}
