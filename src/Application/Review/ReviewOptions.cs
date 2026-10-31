namespace Application.Review;

// The "Today" queue: sizes and the time estimate shown before starting
public class ReviewOptions
{
    public const string SectionName = "Review";

    public int MaxWordsPerSession { get; set; } = 100;
    // Words never reviewed yet join after the due ones, so new material does not crowd out repetition
    public int NewWordsPerSession { get; set; } = 20;
    public int MaxQuestionsPerSession { get; set; } = 30;
    public int SecondsPerWord { get; set; } = 8;
    public int SecondsPerQuestion { get; set; } = 30;
}
