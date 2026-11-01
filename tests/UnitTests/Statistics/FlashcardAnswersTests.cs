using Application.DTOs.Statistics;
using Application.Statistics;
using Domain.Utility.Review;
using Shouldly;

namespace UnitTests.Statistics;

public class FlashcardAnswersTests
{
    private static readonly DateOnly Today = new(2026, 10, 25);
    private static readonly DateTime Noon = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Apple = Guid.NewGuid();
    private static readonly Guid Pear = Guid.NewGuid();
    private static readonly Guid Plum = Guid.NewGuid();

    private static FlashcardAnswerRow Row(Guid entry, ReviewRating rating, DateTime at) => new(entry, rating, at);

    private static FlashcardAnswersDto Summarize(IEnumerable<FlashcardAnswerRow> rows, int days = 7, int hardest = 5) =>
        FlashcardAnswers.Summarize(rows, TimeZoneInfo.Utc, Today, days, hardest);

    [Fact]
    public void Summarize_CountsTheButtons_NotTheManualChanges()
    {
        var result = Summarize([
            Row(Apple, ReviewRating.Again, Noon),
            Row(Apple, ReviewRating.Hard, Noon),
            Row(Apple, ReviewRating.Good, Noon),
            Row(Pear, ReviewRating.Easy, Noon),
            Row(Pear, ReviewRating.Manual, Noon),
        ]);

        result.DontKnow.ShouldBe(1);
        result.Unsure.ShouldBe(1);
        result.Know.ShouldBe(2);
    }

    [Fact]
    public void Summarize_ReturnsEveryDayOfTheWindow_EndingToday()
    {
        var result = Summarize([Row(Apple, ReviewRating.Good, Noon.AddDays(-2))], days: 7);

        result.Days.Count.ShouldBe(7);
        result.Days[0].Date.ShouldBe(Today.AddDays(-6));
        result.Days[^1].Date.ShouldBe(Today);
        result.Days.Single(x => x.Know > 0).Date.ShouldBe(Today.AddDays(-2));
    }

    [Fact]
    public void Summarize_SkipsAnswersBeforeTheWindow()
    {
        var result = Summarize([Row(Apple, ReviewRating.Again, Noon.AddDays(-7))], days: 7);

        result.DontKnow.ShouldBe(0);
        result.HardestWords.ShouldBeEmpty();
    }

    [Fact]
    public void Summarize_PutsAnswersOnTheUsersOwnDay()
    {
        var kyiv = UserTimeZone.Resolve("Europe/Kyiv");
        // 23:30 UTC is already the next morning in Kyiv
        var lateEvening = new DateTime(2026, 10, 24, 23, 30, 0, DateTimeKind.Utc);

        var result = FlashcardAnswers.Summarize([Row(Apple, ReviewRating.Good, lateEvening)], kyiv, Today, 7, 5);

        result.Days.Single(x => x.Know > 0).Date.ShouldBe(Today);
    }

    [Fact]
    public void Summarize_HardestWords_MostMissedFirst_OnlyMissedOnes()
    {
        var result = Summarize([
            Row(Apple, ReviewRating.Again, Noon),
            Row(Pear, ReviewRating.Again, Noon),
            Row(Pear, ReviewRating.Again, Noon),
            Row(Plum, ReviewRating.Good, Noon),
        ]);

        result.HardestWords.Select(x => x.EntryId).ShouldBe([Pear, Apple]);
        result.HardestWords[0].DontKnow.ShouldBe(2);
    }

    [Fact]
    public void Summarize_HardestWords_EqualMisses_FewerAnswersFirst_AndLimited()
    {
        var result = Summarize([
            Row(Apple, ReviewRating.Again, Noon),
            Row(Apple, ReviewRating.Good, Noon),
            Row(Pear, ReviewRating.Again, Noon),
            Row(Plum, ReviewRating.Again, Noon),
            Row(Plum, ReviewRating.Hard, Noon),
            Row(Plum, ReviewRating.Good, Noon),
        ], hardest: 2);

        result.HardestWords.Select(x => x.EntryId).ShouldBe([Pear, Apple]);
    }
}
