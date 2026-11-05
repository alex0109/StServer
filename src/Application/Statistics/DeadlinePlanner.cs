using Application.DTOs.Statistics;
using Domain.Utility.Flashcard;

namespace Application.Statistics;

// Turns what is left of a material into today's work. New items are spread evenly up to the date;
// reviews come from the scheduler as they are, so the plan never fights it
public static class DeadlinePlanner
{
    private const int SecondsPerDueWord = 15;
    private const int SecondsPerNewWord = 25;
    private const int SecondsPerDueQuestion = 45;
    private const int SecondsPerNewQuestion = 60;
    private const int TightMinutesPerDay = 60;

    public static DeadlinePlanDto Build(
        DeadlineMaterialRow material,
        IReadOnlyCollection<DeadlineItemRow> items,
        DateTime utcNow,
        TimeZoneInfo timeZone)
    {
        var today = UserTimeZone.LocalDate(utcNow, timeZone);
        var endOfToday = UserTimeZone.EndOfLocalDayUtc(today, timeZone);
        var endOfDeadline = UserTimeZone.EndOfLocalDayUtc(material.Deadline, timeZone);
        var daysLeft = material.Deadline.DayNumber - today.DayNumber;
        var isPassed = daysLeft < 0;

        var words = Count(items, DeadlineItemKind.Word, endOfToday, endOfDeadline);
        var questions = Count(items, DeadlineItemKind.Question, endOfToday, endOfDeadline);

        var learningDays = LearningDays(daysLeft);
        var newWordsToday = isPassed ? 0 : DivideUp(words.New, learningDays);
        var newQuestionsToday = isPassed ? 0 : DivideUp(questions.New, learningDays);

        var seconds = isPassed
            ? 0
            : words.Due * SecondsPerDueWord
              + newWordsToday * SecondsPerNewWord
              + questions.Due * SecondsPerDueQuestion
              + newQuestionsToday * SecondsPerNewQuestion;
        var minutesToday = DivideUp(seconds, 60);

        var total = words.Total + questions.Total;
        var isClear = words.New + words.Due + words.Weak + questions.New + questions.Due + questions.Weak == 0;

        return new DeadlinePlanDto
        {
            MaterialId = material.MaterialId,
            Title = material.Title,
            Deadline = material.Deadline,
            DaysLeft = daysLeft,
            Pace = isPassed ? DeadlinePace.Passed
                : total == 0 ? DeadlinePace.Empty
                : isClear ? DeadlinePace.Ready
                : minutesToday > TightMinutesPerDay ? DeadlinePace.Tight
                : DeadlinePace.OnTrack,
            TotalWords = words.Total,
            NewWords = words.New,
            DueWords = words.Due,
            WeakWords = words.Weak,
            TotalQuestions = questions.Total,
            NewQuestions = questions.New,
            DueQuestions = questions.Due,
            WeakQuestions = questions.Weak,
            NewWordsToday = newWordsToday,
            NewQuestionsToday = newQuestionsToday,
            ReviewsPerDay = isPassed ? 0 : DivideUp(words.Upcoming + questions.Upcoming, Math.Max(daysLeft, 1)),
            MinutesToday = minutesToday,
            AttemptRecommended = !isPassed
                                 && questions.Total > 0
                                 && IsAttemptDue(material.LastAttemptAt, today, daysLeft, timeZone),
        };
    }

    // Today and the days before the date; the last of them stays free of new items when there is room,
    // so the eve of the date goes to reviews
    private static int LearningDays(int daysLeft) => daysLeft switch
    {
        <= 1 => 1,
        2 => 2,
        _ => daysLeft - 1
    };

    // The closer the date, the more often a full run shows what is still weak
    private static bool IsAttemptDue(DateTime? lastAttemptAt, DateOnly today, int daysLeft, TimeZoneInfo timeZone)
    {
        if (lastAttemptAt is null) return true;

        var everyDays = daysLeft switch
        {
            <= 3 => 1,
            <= 14 => 3,
            _ => 7
        };

        return today.DayNumber - UserTimeZone.LocalDate(lastAttemptAt.Value, timeZone).DayNumber >= everyDays;
    }

    private static Counts Count(
        IEnumerable<DeadlineItemRow> items,
        DeadlineItemKind kind,
        DateTime endOfToday,
        DateTime endOfDeadline)
    {
        var ofKind = items.Where(x => x.Kind == kind).ToList();
        var studied = ofKind.Where(x => x.State != FlashcardState.New).ToList();

        return new Counts(
            ofKind.Count,
            ofKind.Count - studied.Count,
            studied.Count(x => x.Due <= endOfToday),
            // Still being learned, or forgotten before and coming back before the date
            studied.Count(x => x.State is FlashcardState.Learning or FlashcardState.Relearning
                               || (x.Lapses > 0 && x.Due <= endOfDeadline)),
            studied.Count(x => x.Due <= endOfDeadline));
    }

    private static int DivideUp(int value, int by) => (value + by - 1) / by;

    private sealed record Counts(int Total, int New, int Due, int Weak, int Upcoming);
}
