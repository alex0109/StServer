namespace Application.Essays;

// The four things an essay is scored on, each from 0 to 100
public enum EssayCriterion
{
    // Answers every part of the task, keeps a clear position, ideas relevant and supported
    TaskResponse,
    // Logical order, paragraphs with one central idea, natural linking
    Coherence,
    // Range, precision and spelling of words
    Vocabulary,
    // Range and accuracy of grammar, punctuation
    Grammar,
}

public enum EssayCorrectionCategory
{
    Grammar,
    Vocabulary,
    Spelling,
    Punctuation,
    Style,
}

// Band is set by IELTS checks only: the 0-9 band the score was taken from
public record EssayCriterionScore(EssayCriterion Criterion, int Score, string Comment, int? Band = null);

// "Original" is copied from the essay, so the editor can find and mark it
public record EssayCorrection(string Original, string Correction, string Explanation, EssayCorrectionCategory Category);

// The last check of an essay, kept with the note. Score is the mean of the criteria
public record EssayReview
{
    public required int Score { get; init; }
    public required List<EssayCriterionScore> Criteria { get; init; }
    public required List<string> Strengths { get; init; }
    public required List<string> Improvements { get; init; }
    public required List<EssayCorrection> Corrections { get; init; }
    public required int WordCount { get; init; }
    // Language code of the feedback, "question" when it is written in the essay's language
    public required string Language { get; init; }
    // Only the first part of a very long essay was checked
    public required bool IsTextTruncated { get; init; }
    public required DateTime CheckedAt { get; init; }
    // The exam format it was checked for ("Ielts/B2/Letter"); null for the general check
    public string? Format { get; init; }
    // The estimated result on that exam's scale: an IELTS band like "6.5", or a level like "B1" or "below_A2"
    public string? ExamResult { get; init; }
}
