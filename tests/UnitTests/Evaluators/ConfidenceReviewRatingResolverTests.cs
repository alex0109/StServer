using Application.Evaluators;
using Domain.Utility.Result;
using Domain.Utility.Review;
using Shouldly;

namespace UnitTests.Evaluators;

public class ConfidenceReviewRatingResolverTests
{
    private readonly ConfidenceReviewRatingResolver _sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData(ConfidenceLevel.Low)]
    [InlineData(ConfidenceLevel.High)]
    public void Resolve_WrongAnswer_IsAgain(ConfidenceLevel? confidence)
    {
        _sut.Resolve(false, confidence).ShouldBe(ReviewRating.Again);
    }

    [Theory]
    [InlineData(ConfidenceLevel.Low, ReviewRating.Hard)]
    [InlineData(ConfidenceLevel.Medium, ReviewRating.Good)]
    [InlineData(ConfidenceLevel.High, ReviewRating.Easy)]
    [InlineData(null, ReviewRating.Good)]
    public void Resolve_CorrectAnswer_DependsOnConfidence(ConfidenceLevel? confidence, ReviewRating expected)
    {
        _sut.Resolve(true, confidence).ShouldBe(expected);
    }
}
