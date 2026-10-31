using Application.DTOs.Statistics;

namespace Application.DTOs.Review;

// What the "Today" queue holds, counted to the end of the user's local day
public class ReviewTodayDto
{
    public required string LocalDate { get; init; }
    public required int DueWords { get; init; }
    public required int NewWords { get; init; }
    public required int DueQuestions { get; init; }
    // Due before today started: these were missed
    public required int OverdueWords { get; init; }
    public required int OverdueQuestions { get; init; }
    // Words in a session: due ones plus at most this many new ones
    public required int WordsInSession { get; init; }
    public required int QuestionsInSession { get; init; }
    public required int EstimatedMinutes { get; init; }
    public required List<DictionaryDueDto> Dictionaries { get; init; }
    public required List<MaterialDueDto> Materials { get; init; }

    // Reviews of words and questions done today, attempt answers included
    public int ReviewsDone { get; init; }
    public int? DailyGoal { get; init; }
    // What the user's daily limits still allow, null when there is no limit
    public int? NewWordsLeft { get; init; }
    public int? ReviewsLeft { get; init; }
}
