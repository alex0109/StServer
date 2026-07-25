using Application.DTOs.Question;
using Application.Evaluators;
using Shouldly;

namespace UnitTests.Evaluators;

public class ExactValidationStepTests
{
    private readonly ExactValidationStep _sut = new();

    [Fact]
    public void Priority_ShouldReturnOne()
    {
        var result = _sut.Priority;

        result.ShouldBe(1);
    }


    [Fact]
    public void Evaluate_WhenAnswersMatch_ReturnsCorrectResult()
    {
        var result = _sut.Evaluate(
            correctAnswer: "Paris",
            userAnswer: "Paris");

        result.ShouldNotBeNull();

        result.IsCorrect.ShouldBeTrue();
        result.Score.ShouldBe(100);
        result.Method.ShouldBe(EvaluationMethod.Exact);
    }


    [Fact]
    public void Evaluate_WhenAnswersDoNotMatch_ReturnsNull()
    {
        var result = _sut.Evaluate(
            correctAnswer: "Paris",
            userAnswer: "London");

        result.ShouldBeNull();
    }


    [Fact]
    public void Evaluate_WhenAnswersHaveDifferentCase_ReturnsNull()
    {
        var result = _sut.Evaluate(
            correctAnswer: "Paris",
            userAnswer: "paris");

        result.ShouldBeNull();
    }
}