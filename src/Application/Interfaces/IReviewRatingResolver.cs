using Domain.Utility.Result;
using Domain.Utility.Review;

namespace Application.Interfaces;

public interface IReviewRatingResolver
{
    ReviewRating Resolve(bool isCorrect, ConfidenceLevel? confidenceLevel);
}
