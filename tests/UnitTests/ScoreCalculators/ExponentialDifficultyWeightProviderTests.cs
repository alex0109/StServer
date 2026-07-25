using Application.ScoreCalculators;
using Domain.Utility.Question;
using Shouldly;

namespace UnitTests.ScoreCalculators;

public class ExponentialDifficultyWeightProviderTests
{
    private readonly ExponentialDifficultyWeightProvider _sut = new();

    [Theory]
    [InlineData(QuestionDifficulty.Easy, 1)]
    [InlineData(QuestionDifficulty.Medium, 2)]
    [InlineData(QuestionDifficulty.Hard, 4)]
    public void GetWeight_ReturnsExpectedWeight(
        QuestionDifficulty difficulty,
        double expected)
    {
        var result = _sut.GetWeight(difficulty);

        result.ShouldBe(expected);
    }
}