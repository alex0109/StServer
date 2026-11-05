namespace Application.DTOs.Statistics;

public class StudyCalendarDto
{
    public required DateOnly Today { get; init; }
    // Days with any study in the requested range
    public required List<DateOnly> StudyDays { get; init; }
    // The days of the running streak, freezes included
    public required List<DateOnly> StreakDays { get; init; }
    public required List<CalendarDeadlineDto> Deadlines { get; init; }
}

public class CalendarDeadlineDto
{
    public required DateOnly Date { get; init; }
    public required Guid MaterialId { get; init; }
    public required string Title { get; init; }
}
