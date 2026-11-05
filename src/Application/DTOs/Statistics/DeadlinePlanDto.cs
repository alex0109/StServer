namespace Application.DTOs.Statistics;

public enum DeadlinePace
{
    // Nothing to learn yet: no words and no questions
    Empty,
    // Everything is learned and nothing is due
    Ready,
    OnTrack,
    // More than an hour a day is needed to make it
    Tight,
    // The date has passed
    Passed
}

// What is left before a deadline and how much of it to do today
public class DeadlinePlanDto
{
    public required Guid MaterialId { get; init; }
    public required string Title { get; init; }
    public required DateOnly Deadline { get; init; }
    // 0 means the date is today, negative that it has passed
    public required int DaysLeft { get; init; }
    public required DeadlinePace Pace { get; init; }

    public required int TotalWords { get; init; }
    public required int NewWords { get; init; }
    public required int DueWords { get; init; }
    public required int WeakWords { get; init; }

    public required int TotalQuestions { get; init; }
    public required int NewQuestions { get; init; }
    public required int DueQuestions { get; init; }
    public required int WeakQuestions { get; init; }

    // Today's share of what was never studied, spread evenly over the days left
    public required int NewWordsToday { get; init; }
    public required int NewQuestionsToday { get; init; }
    // Reviews already scheduled up to the date, as a daily average
    public required int ReviewsPerDay { get; init; }
    public required int MinutesToday { get; init; }
    // A full run through the questions is due, by how close the date is
    public required bool AttemptRecommended { get; init; }
}
