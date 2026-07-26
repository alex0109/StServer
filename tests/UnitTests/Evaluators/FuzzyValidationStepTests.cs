using Application.DTOs.Question;
using Application.Evaluators;
using Shouldly;

namespace UnitTests.Evaluators;

public class FuzzyValidationStepTests
{
    private readonly FuzzyValidationStep _sut = new();


    [Fact]
    public void Priority_ShouldReturnTwo()
    {
        _sut.Priority.ShouldBe(2);
    }


    [Fact]
    public void Evaluate_ExactMatch_ReturnsFullScore()
    {
        var result = _sut.Evaluate(
            "Paris",
            "Paris");

        result.ShouldNotBeNull();

        result.IsCorrect.ShouldBeTrue();
        result.Score.ShouldBe(100);
        result.Method.ShouldBe(EvaluationMethod.Fuzzy);
    }


    [Fact]
    public void Evaluate_DifferentWordOrder_ReturnsCorrect()
    {
        var result = _sut.Evaluate(
            "Paris is the capital of France",
            "France capital is Paris");

        result.ShouldNotBeNull();

        result.IsCorrect.ShouldBeTrue();
        result.Method.ShouldBe(EvaluationMethod.Fuzzy);
        result.Score.ShouldBeGreaterThanOrEqualTo(85);
    }


    [Fact]
    public void Evaluate_SimilarAnswer_ReturnsCorrect()
    {
        var result = _sut.Evaluate(
            "The Earth revolves around the Sun",
            "Earth revolves around Sun");

        result.ShouldNotBeNull();

        result.IsCorrect.ShouldBeTrue();
        result.Method.ShouldBe(EvaluationMethod.Fuzzy);
        result.Score.ShouldBeGreaterThanOrEqualTo(85);
    }


    [Fact]
    public void Evaluate_CompletelyDifferentAnswer_ReturnsNull()
    {
        var result = _sut.Evaluate(
            "The Earth revolves around the Sun",
            "The Moon is made of cheese");

        result.ShouldBeNull();
    }


    [Fact]
    public void Evaluate_SlightTypo_ReturnsCorrect()
    {
        var result = _sut.Evaluate(
            "Programming language CSharp",
            "Programming language CShar");

        result.ShouldNotBeNull();

        result.IsCorrect.ShouldBeTrue();
        result.Method.ShouldBe(EvaluationMethod.Fuzzy);
    }


    [Fact]
    public void Evaluate_DifferentCase_ReturnsCorrect()
    {
        var result = _sut.Evaluate(
            "JavaScript",
            "javascript");

        result.ShouldNotBeNull();

        result.IsCorrect.ShouldBeTrue();
        result.Score.ShouldBe(100);
    }


    [Fact]
    public void Evaluate_ExtraWords_ReturnsNullWhenSimilarityIsHigh()
    {
        var result = _sut.Evaluate(
            "ASP.NET Core",
            "ASP.NET Core framework");

        result.ShouldBeNull();
    }


    [Fact]
    public void Evaluate_ShortUnrelatedAnswer_ReturnsNull()
    {
        var result = _sut.Evaluate(
            "Object oriented programming principles",
            "cat");

        result.ShouldBeNull();
    }


    [Fact]
    public void Evaluate_EmptyUserAnswer_ReturnsNull()
    {
        var result = _sut.Evaluate(
            "Paris",
            "");

        result.ShouldBeNull();
    }


    [Fact]
    public void Evaluate_EmptyCorrectAnswer_ReturnsNull()
    {
        var result = _sut.Evaluate(
            "",
            "Paris");

        result.ShouldBeNull();
    }


    [Fact]
    public void Evaluate_BothEmptyAnswers_ReturnsNull()
    {
        var result = _sut.Evaluate(
            "",
            "");

        result.ShouldBeNull();
    }


    [Fact]
    public void Evaluate_WhitespaceOnlyAnswers_ReturnsNull()
    {
        var result = _sut.Evaluate(
            "   ",
            "   ");

        result.ShouldBeNull();
    }
}