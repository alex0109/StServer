using Application.Evaluators;
using Application.Services;
using Shouldly;

namespace UnitTests.Evaluators;

public class FuzzyAnswerMatcherTests
{
    private readonly AnswerNormalizer _normalizer = new();

    private FuzzyMatch Compare(string correct, string user) =>
        FuzzyAnswerMatcher.Compare(_normalizer.Normalize(correct), _normalizer.Normalize(user));

    [Theory]
    [InlineData("Photosynthesis", "photosyntesis")]
    [InlineData("фотосинтез", "фотосинтес")]
    [InlineData("The Earth revolves around the Sun", "Earth revolves around Sun")]
    [InlineData("photosynthesis", "photo synthesis")]
    [InlineData("There are 3 states of matter", "There are three states of matter")]
    [InlineData("Має п'ять пелюсток", "має 5 пелюсток")]
    [InlineData("World War II ended in 1945", "world war ii ended in 1945!")]
    [InlineData("mitochondria is the powerhouse of the cell", "the powerhouse of the cell is mitochondria")]
    public void SameAnswer_Matches(string correct, string user)
    {
        Compare(correct, user).IsMatch.ShouldBeTrue();
    }

    [Theory]
    [InlineData("World War II ended in 1945", "World War II ended in 1946")]
    [InlineData("The speed limit is 50 km/h", "The speed limit is 60 km/h")]
    [InlineData("A triangle has 3 sides", "A triangle has four sides")]
    public void AnotherNumber_DoesNotMatch(string correct, string user)
    {
        var match = Compare(correct, user);

        match.FactsMatch.ShouldBeFalse();
        match.IsMatch.ShouldBeFalse();
        // Close enough to be a doubtful case for the meaning check
        match.Score.ShouldBeGreaterThanOrEqualTo(40);
    }

    [Theory]
    [InlineData("Water is a compound", "Water is not a compound")]
    [InlineData("Whales are mammals", "Whales aren't mammals")]
    [InlineData("Кити є ссавцями", "Кити не є ссавцями")]
    public void Negation_DoesNotMatch(string correct, string user)
    {
        Compare(correct, user).IsMatch.ShouldBeFalse();
    }

    [Theory]
    [InlineData("ASP.NET Core", "ASP.NET Core framework")]
    [InlineData("house", "horse")]
    [InlineData("Object oriented programming principles", "cat")]
    public void DifferentAnswer_DoesNotMatch(string correct, string user)
    {
        Compare(correct, user).IsMatch.ShouldBeFalse();
    }

    [Fact]
    public void EmptyAnswer_ScoresZero()
    {
        Compare("Paris", "").ShouldBe(new FuzzyMatch(0, false));
    }
}
