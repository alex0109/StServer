using Application.Interfaces;
using Application.Statistics;
using Domain.Utility.Result;
using Domain.Utility.Review;

namespace Application.Evaluators;

// Turns a graded answer into an FSRS rating: a wrong answer is a lapse,
// confidence of a correct answer tells how easy the recall was
public class ConfidenceReviewRatingResolver : IReviewRatingResolver
{
    public ReviewRating Resolve(bool isCorrect, ConfidenceLevel? confidenceLevel, int answerChanges = 0)
    {
        if (!isCorrect) return ReviewRating.Again;

        var rating = confidenceLevel switch
        {
            ConfidenceLevel.Low => ReviewRating.Hard,
            ConfidenceLevel.High => ReviewRating.Easy,
            _ => ReviewRating.Good
        };

        // A right answer reached after doubts was not an easy recall, whatever the user said about it
        return AnswerHesitation.Level(answerChanges) switch
        {
            0 => rating,
            1 => rating == ReviewRating.Easy ? ReviewRating.Good : rating,
            _ => ReviewRating.Hard
        };
    }
}
