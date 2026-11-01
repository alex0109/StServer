using Application.DTOs.Statistics;
using Domain.Utility.Review;

namespace Application.Statistics;

public enum FlashcardAnswer
{
    DontKnow,
    Unsure,
    Know
}

// Flashcard reviews counted by the buttons the user pressed, not by FSRS ratings
public static class FlashcardAnswers
{
    // A card sends Again, Hard or Good; Easy is counted as known too. Manual marks a progress
    // changed by hand, nobody answered anything
    public static FlashcardAnswer? FromRating(ReviewRating rating) => rating switch
    {
        ReviewRating.Again => FlashcardAnswer.DontKnow,
        ReviewRating.Hard => FlashcardAnswer.Unsure,
        ReviewRating.Good or ReviewRating.Easy => FlashcardAnswer.Know,
        _ => null
    };

    // Words come without terms: only the few hardest ones are read from the database afterwards
    public static FlashcardAnswersDto Summarize(
        IEnumerable<FlashcardAnswerRow> rows,
        TimeZoneInfo timeZone,
        DateOnly today,
        int days,
        int hardestCount)
    {
        var firstDay = today.AddDays(-(days - 1));
        var answers = rows
            .Select(x => (x.EntryId, Answer: FromRating(x.Rating), Day: UserTimeZone.LocalDate(x.ReviewedAt, timeZone)))
            .Where(x => x.Answer is not null && x.Day >= firstDay && x.Day <= today)
            .Select(x => (x.EntryId, Answer: x.Answer!.Value, x.Day))
            .ToList();

        var byDay = answers.ToLookup(x => x.Day);

        return new FlashcardAnswersDto
        {
            Know = answers.Count(x => x.Answer == FlashcardAnswer.Know),
            Unsure = answers.Count(x => x.Answer == FlashcardAnswer.Unsure),
            DontKnow = answers.Count(x => x.Answer == FlashcardAnswer.DontKnow),
            Days = Enumerable.Range(0, days)
                .Select(offset => firstDay.AddDays(offset))
                .Select(day => new FlashcardAnswersDayDto
                {
                    Date = day,
                    Know = byDay[day].Count(x => x.Answer == FlashcardAnswer.Know),
                    Unsure = byDay[day].Count(x => x.Answer == FlashcardAnswer.Unsure),
                    DontKnow = byDay[day].Count(x => x.Answer == FlashcardAnswer.DontKnow),
                })
                .ToList(),
            HardestWords = answers
                .GroupBy(x => x.EntryId)
                .Select(g => new HardWordDto
                {
                    EntryId = g.Key,
                    DontKnow = g.Count(x => x.Answer == FlashcardAnswer.DontKnow),
                    Answers = g.Count(),
                })
                .Where(x => x.DontKnow > 0)
                // Equal misses: the word with fewer answers overall is missed more often
                .OrderByDescending(x => x.DontKnow)
                .ThenBy(x => x.Answers)
                .Take(hardestCount)
                .ToList(),
        };
    }
}
