namespace Application.DTOs.Review;

// The answers of one local day
public class ReviewDayDto
{
    // "yyyy-MM-dd" in the user's own calendar
    public required string Date { get; init; }
    public int Reviews { get; init; }
    // Answered "know" or right: rated Good or Easy
    public int Remembered { get; init; }
    // Remembered out of all answers, 0-100
    public int Accuracy { get; init; }
    public int Words { get; init; }
    public int Questions { get; init; }
}

// Shown when "Today" is done: today next to the last day before it with any reviews
public class ReviewDaySummaryDto
{
    public required ReviewDayDto Today { get; init; }
    public ReviewDayDto? Previous { get; init; }
}
