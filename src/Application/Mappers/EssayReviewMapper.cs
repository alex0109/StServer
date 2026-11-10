using Application.DTOs.Note;
using Application.Essays;

namespace Application.Mappers;

public static class EssayReviewMapper
{
    public static EssayReviewDto ToDto(EssayReview review) => new()
    {
        Score = review.Score,
        Criteria = review.Criteria
            .Select(x => new EssayCriterionScoreDto { Criterion = x.Criterion, Score = x.Score, Comment = x.Comment })
            .ToList(),
        Strengths = review.Strengths.ToList(),
        Improvements = review.Improvements.ToList(),
        Corrections = review.Corrections
            .Select(x => new EssayCorrectionDto
            {
                Original = x.Original,
                Correction = x.Correction,
                Explanation = x.Explanation,
                Category = x.Category,
            })
            .ToList(),
        WordCount = review.WordCount,
        IsTextTruncated = review.IsTextTruncated,
        CheckedAt = review.CheckedAt,
    };
}
