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
}

public class EssayCriterionScoreDto
{
    public EssayCriterion Criterion { get; init; }
    public int Score { get; init; }
    public required string Comment { get; init; }
}

public class EssayCorrectionDto
{
    public required string Original { get; init; }
    public required string Correction { get; init; }
    public required string Explanation { get; init; }
    public EssayCorrectionCategory Category { get; init; }
}
