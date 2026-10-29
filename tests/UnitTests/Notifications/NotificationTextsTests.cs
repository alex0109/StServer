using Application.DTOs.Notification;
using Application.Notifications;
using Shouldly;

namespace UnitTests.Notifications;

public class NotificationTextsTests
{
    [Theory]
    [InlineData(1, "слово")]
    [InlineData(3, "слова")]
    [InlineData(5, "слів")]
    [InlineData(11, "слів")]
    [InlineData(21, "слово")]
    [InlineData(24, "слова")]
    public void Ukrainian_UsesRightPluralForm(int count, string word)
    {
        var text = NotificationTexts.DailyReview("uk", new DailyReviewData(count, 0, []));

        text.Subject.ShouldEndWith($"{count} {word}");
    }

    [Theory]
    [InlineData("en-GB", "words")]
    [InlineData("nb", "ord")]
    [InlineData("uk", "слова")]
    public void EveryLanguageHasTexts(string language, string expectedWord)
    {
        var daily = NotificationTexts.DailyReview(language, new DailyReviewData(23, 4, []));
        var streak = NotificationTexts.StreakAtRisk(language, new StreakAtRiskData(5));
        var weekly = NotificationTexts.WeeklyDigest(language, new WeeklyDigestData(
            "2026-10-19", "2026-10-25", 5, 7, 3, 20, 80, 70, 76.5, 40,
            [new WeakTopicData("Biology", 50, 4)]));

        daily.Subject.ShouldContain("23");
        daily.Lines[0].ShouldContain(expectedWord);
        streak.Subject.ShouldContain("5");
        weekly.Lines.ShouldContain(x => x.Contains("Biology (50%)") || x.Contains("Biology (50 %)") || x.Contains("Biology (50"));
        NotificationTexts.Footer(language).ShouldNotBeNullOrWhiteSpace();
    }
}
