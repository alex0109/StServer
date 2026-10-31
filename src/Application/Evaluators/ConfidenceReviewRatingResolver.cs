using Application.Interfaces;
using Domain.Utility.Result;
using Domain.Utility.Review;

namespace Application.Evaluators;

// Turns a graded answer into an FSRS rating: a wrong answer is a lapse,
// confidence of a correct answer tells how easy the recall was
public class ConfidenceReviewRatingResolver : IReviewRatingResolver
{
    public ReviewRating Resolve(bool isCorrect, ConfidenceLevel? confidenceLevel)
    {
        if (!isCorrect) return ReviewRating.Again;

        return confidenceLevel switch
        {
            ConfidenceLevel.Low => ReviewRating.Hard,
            ConfidenceLevel.High => ReviewRating.Easy,
            _ => ReviewRating.Good
        };
    }
}
