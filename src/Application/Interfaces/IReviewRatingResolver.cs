using Domain.Utility.Result;
using Domain.Utility.Review;

namespace Application.Interfaces;

public interface IReviewRatingResolver
{
    // answerChanges: how often the choice changed before it was saved, a sign of doubt
    ReviewRating Resolve(bool isCorrect, ConfidenceLevel? confidenceLevel, int answerChanges = 0);
}
