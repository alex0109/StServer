using Application.DTOs.Statistics;
using Application.Evaluators;
using Application.Statistics;
using Domain.Utility.Flashcard;
using Domain.Utility.Result;
using Domain.Utility.Review;
using Shouldly;

namespace UnitTests.Statistics;

public class DeadlinePlannerTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly Guid MaterialId = Guid.NewGuid();

    private static DeadlineMaterialRow Material(int daysLeft, DateTime? lastAttempt = null) =>
        new(MaterialId, "Biology", Today.AddDays(daysLeft), lastAttempt);

    private static DeadlineItemRow Word(FlashcardState state = FlashcardState.New, DateTime? due = null, int lapses = 0) =>
        new(MaterialId, DeadlineItemKind.Word, state, due, lapses);

    private static DeadlineItemRow Question(FlashcardState state = FlashcardState.New, DateTime? due = null, int lapses = 0) =>
        new(MaterialId, DeadlineItemKind.Question, state, due, lapses);

    private static DeadlinePlanDto Build(DeadlineMaterialRow material, params DeadlineItemRow[] items) =>
        DeadlinePlanner.Build(material, items, Now, TimeZoneInfo.Utc);

    [Fact]
    public void NewItems_AreSpreadOverTheDays_KeepingTheEveOfTheDateFree()
    {
        var plan = Build(Material(daysLeft: 11), Enumerable.Range(0, 25).Select(_ => Word()).ToArray());

        plan.DaysLeft.ShouldBe(11);
        plan.NewWords.ShouldBe(25);
        // Ten days for 25 words: rounded up, so nothing is left for the last day
        plan.NewWordsToday.ShouldBe(3);
    }

    [Fact]
    public void DateTomorrow_EverythingNewIsForToday()
    {
        var plan = Build(Material(daysLeft: 1), Word(), Word(), Question());

        plan.NewWordsToday.ShouldBe(2);
        plan.NewQuestionsToday.ShouldBe(1);
    }

    [Fact]
    public void DueItems_CountOnlyThoseOfToday_AndAddUpToMinutes()
    {
        var plan = Build(
            Material(daysLeft: 5),
            Word(FlashcardState.Review, Now.AddHours(-1)),
            Word(FlashcardState.Review, Now.AddDays(3)),
            Question(FlashcardState.Review, Now.AddHours(1)));

        plan.DueWords.ShouldBe(1);
        plan.DueQuestions.ShouldBe(1);
        // 15 s for a word and 45 s for a question
        plan.MinutesToday.ShouldBe(1);
        plan.Pace.ShouldBe(DeadlinePace.OnTrack);
    }

    [Fact]
    public void Weak_IsWhatIsStillLearnedOrForgottenBeforeTheDate()
    {
        var plan = Build(
            Material(daysLeft: 3),
            Word(FlashcardState.Relearning, Now.AddDays(1), lapses: 1),
            Word(FlashcardState.Review, Now.AddDays(2), lapses: 2),
            Word(FlashcardState.Review, Now.AddDays(30), lapses: 2),
            Word(FlashcardState.Review, Now.AddDays(1)));

        plan.WeakWords.ShouldBe(2);
    }

    [Fact]
    public void AllLearnedAndNothingDue_IsReady()
    {
        var plan = Build(Material(daysLeft: 3), Word(FlashcardState.Review, Now.AddDays(10)));

        plan.Pace.ShouldBe(DeadlinePace.Ready);
        plan.MinutesToday.ShouldBe(0);
    }

    [Fact]
    public void NothingToLearn_IsEmpty() =>
        Build(Material(daysLeft: 3)).Pace.ShouldBe(DeadlinePace.Empty);

    [Fact]
    public void PassedDate_AsksForNothing()
    {
        var plan = Build(Material(daysLeft: -2), Word(), Question(FlashcardState.Review, Now.AddHours(-1)));

        plan.DaysLeft.ShouldBe(-2);
        plan.Pace.ShouldBe(DeadlinePace.Passed);
        plan.NewWordsToday.ShouldBe(0);
        plan.MinutesToday.ShouldBe(0);
        plan.AttemptRecommended.ShouldBeFalse();
    }

    [Fact]
    public void TooMuchForADay_IsTight()
    {
        var plan = Build(Material(daysLeft: 1), Enumerable.Range(0, 70).Select(_ => Question()).ToArray());

        plan.MinutesToday.ShouldBe(70);
        plan.Pace.ShouldBe(DeadlinePace.Tight);
    }

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(10, 2, false)]
    [InlineData(10, 3, true)]
    [InlineData(30, 6, false)]
    [InlineData(30, 7, true)]
    public void Attempt_IsRecommendedMoreOftenCloserToTheDate(int daysLeft, int daysSinceAttempt, bool expected)
    {
        var plan = Build(Material(daysLeft, Now.AddDays(-daysSinceAttempt)), Question());

        plan.AttemptRecommended.ShouldBe(expected);
    }

    [Fact]
    public void Days_AreCountedInTheUsersOwnCalendar()
    {
        var kyiv = UserTimeZone.Resolve("Europe/Kyiv");
        var lateUtc = new DateTime(2026, 11, 5, 23, 30, 0, DateTimeKind.Utc);

        // Already the 6th in Kyiv
        DeadlinePlanner.Build(Material(daysLeft: 3), [], lateUtc, kyiv).DaysLeft.ShouldBe(2);
    }
}

public class AnswerHesitationTests
{
    private readonly ConfidenceReviewRatingResolver _sut = new();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(9, 3)]
    [InlineData(-1, 0)]
    public void Level_GrowsWithChanges(int changes, int level) =>
        AnswerHesitation.Level(changes).ShouldBe(level);

    [Theory]
    [InlineData(ConfidenceLevel.High, 0, ReviewRating.Easy)]
    [InlineData(ConfidenceLevel.High, 1, ReviewRating.Good)]
    [InlineData(ConfidenceLevel.High, 2, ReviewRating.Hard)]
    [InlineData(ConfidenceLevel.Medium, 1, ReviewRating.Good)]
    [InlineData(ConfidenceLevel.Medium, 3, ReviewRating.Hard)]
    [InlineData(ConfidenceLevel.Low, 1, ReviewRating.Hard)]
    public void CorrectAnswer_AfterChanges_RatesLower(ConfidenceLevel confidence, int changes, ReviewRating expected) =>
        _sut.Resolve(true, confidence, changes).ShouldBe(expected);

    [Fact]
    public void WrongAnswer_IsAlwaysAgain() =>
        _sut.Resolve(false, ConfidenceLevel.High, 5).ShouldBe(ReviewRating.Again);
}
