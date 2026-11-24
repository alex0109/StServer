using Application.Essays;

namespace Application.DTOs.Note;

public class EssayReviewDto
{
    public int Score { get; init; }
    public required List<EssayCriterionScoreDto> Criteria { get; init; }
    public required List<string> Strengths { get; init; }
    public required List<string> Improvements { get; init; }
    public required List<EssayCorrectionDto> Corrections { get; init; }
    public int WordCount { get; init; }
    public bool IsTextTruncated { get; init; }
    public DateTime CheckedAt { get; init; }
    // Exam checks only: the format checked for and the estimated result on the exam's scale
    public string? Format { get; init; }
    public string? ExamResult { get; init; }
}

public class EssayCriterionScoreDto
{
    public EssayCriterion Criterion { get; init; }
    public int Score { get; init; }
    public required string Comment { get; init; }
    // IELTS checks only: the band from 0 to 9
    public int? Band { get; init; }
}

public class EssayCorrectionDto
{
    public required string Original { get; init; }
    public required string Correction { get; init; }
    public required string Explanation { get; init; }
    public EssayCorrectionCategory Category { get; init; }
}
