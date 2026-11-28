using Application.DTOs.Review;
using Application.Statistics;
using Domain.Utility.Review;

namespace Application.Review;

// One answer from the review logs: a word card or a question, from "Today" or a finished attempt
public sealed record ReviewLogPoint(DateTime ReviewedAt, ReviewRating Rating, bool IsWord);

// How today went, next to the last day before it with any reviews
public static class ReviewDaySummary
{
    // How far back the day to compare with is looked for
    public const int LookBackDays = 90;

    public static ReviewDaySummaryDto Build(IEnumerable<ReviewLogPoint> points, DateOnly today, TimeZoneInfo timeZone)
    {
        // A word marked as known by hand is not an answer, so it says nothing about how the day went
        var days = points
            .Where(x => x.Rating != ReviewRating.Manual)
            .GroupBy(x => UserTimeZone.LocalDate(x.ReviewedAt, timeZone))
            .ToDictionary(x => x.Key, x => x.ToList());

        var previous = days.Keys.Where(x => x < today).OrderDescending().Cast<DateOnly?>().FirstOrDefault();

        return new ReviewDaySummaryDto
        {
            Today = Day(today, days.GetValueOrDefault(today) ?? []),
            Previous = previous is { } date ? Day(date, days[date]) : null,
        };
    }

    // Good and Easy are remembered; Hard came back only with effort, Again not at all
    public static bool IsRemembered(ReviewRating rating) => rating is ReviewRating.Good or ReviewRating.Easy;

    private static ReviewDayDto Day(DateOnly date, List<ReviewLogPoint> points)
    {
        var remembered = points.Count(x => IsRemembered(x.Rating));

        return new ReviewDayDto
        {
            Date = date.ToString("yyyy-MM-dd"),
            Reviews = points.Count,
            Remembered = remembered,
            Accuracy = points.Count == 0 ? 0 : (int)Math.Round(remembered * 100.0 / points.Count),
            Words = points.Count(x => x.IsWord),
            Questions = points.Count(x => !x.IsWord),
        };
    }
}
