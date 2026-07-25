using Application.DTOs.Question;
using Application.Interfaces;
using Application.Services;
using Moq;
using Shouldly;

namespace UnitTests.Services;

public class AnswerEvaluationServiceTests
{
    private readonly Mock<IAnswerNormalizer> _normalizer = new();

    private AnswerEvaluationService CreateService(
        params IAnswerValidationStep[] steps)
    {
        return new AnswerEvaluationService(
            _normalizer.Object,
            steps);
    }

    [Fact]
    public void EvaluateAnswer_FirstStepReturnsResult_ReturnsIt()
    {
        var step = new Mock<IAnswerValidationStep>();

        step.Setup(x => x.Priority)
            .Returns(1);

        var expected = new AnswerEvaluationResult
        {
            IsCorrect = true,
            Score = 100,
            Method = EvaluationMethod.Exact
        };

        step.Setup(x =>
                x.Evaluate("answer", "answer"))
            .Returns(expected);

        _normalizer.Setup(x =>
                x.Normalize(It.IsAny<string>()))
            .Returns<string>(x => x);
        
        var sut = CreateService(step.Object);
        
        var result =
            sut.EvaluateAnswer(
                "answer",
                "answer");
        
        result.ShouldBe(expected);

        step.Verify(x =>
            x.Evaluate("answer", "answer"),
            Times.Once);
    }
    
    [Fact]
    public void EvaluateAnswer_NormalizesAnswersBeforeValidation()
    {
        var step = new Mock<IAnswerValidationStep>();
        
        step.Setup(x => x.Priority)
            .Returns(1);
        
        _normalizer.Setup(x =>
                x.Normalize("  Test  "))
            .Returns("test");
        
        _normalizer.Setup(x =>
                x.Normalize(" TEST "))
            .Returns("test");

        step.Setup(x =>
                x.Evaluate("test", "test"))
            .Returns(new AnswerEvaluationResult
            {
                IsCorrect = true,
                Score = 100,
                Method = EvaluationMethod.Exact
            });

        var sut = CreateService(step.Object);

        var result =
            sut.EvaluateAnswer(
                "  Test  ",
                " TEST ");

        result.IsCorrect.ShouldBeTrue();
        
        step.Verify(x =>
            x.Evaluate("test", "test"),
            Times.Once);
    }

    [Fact]
    public void EvaluateAnswer_StepsOrderedByPriority()
    {
        var executionOrder = new List<int>();
        
        var first = new Mock<IAnswerValidationStep>();
        first.Setup(x => x.Priority)
            .Returns(2);

        first.Setup(x =>
                x.Evaluate(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .Callback(() => executionOrder.Add(2))
            .Returns(new AnswerEvaluationResult
            {
                IsCorrect = true,
                Score = 80,
                Method = EvaluationMethod.Fuzzy
            });

        var second = new Mock<IAnswerValidationStep>();
        second.Setup(x => x.Priority)
            .Returns(1);

        second.Setup(x =>
                x.Evaluate(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .Callback(() => executionOrder.Add(1))
            .Returns(null as AnswerEvaluationResult);
        
        _normalizer.Setup(x =>
                x.Normalize(It.IsAny<string>()))
            .Returns<string>(x => x);
        
        var sut =
            CreateService(
                first.Object,
                second.Object);

        sut.EvaluateAnswer(
            "a",
            "b");
        
        executionOrder
            .ShouldBe([1,2]);
    }

    [Fact]
    public void EvaluateAnswer_FirstSuccessfulStepStopsExecution()
    {
        var first = new Mock<IAnswerValidationStep>();
        var second = new Mock<IAnswerValidationStep>();
        
        first.Setup(x => x.Priority)
            .Returns(1);

        second.Setup(x => x.Priority)
            .Returns(2);
        
        first.Setup(x =>
                x.Evaluate(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .Returns(new AnswerEvaluationResult
            {
                IsCorrect = true,
                Score = 90,
                Method = EvaluationMethod.Fuzzy
            });
        
        var sut =
            CreateService(
                first.Object,
                second.Object);

        var result =
            sut.EvaluateAnswer(
                "a",
                "b");
        
        result.Method
            .ShouldBe(EvaluationMethod.Fuzzy);
        
        second.Verify(x =>
            x.Evaluate(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void EvaluateAnswer_AllStepsFail_ReturnsNone()
    {
        var step = new Mock<IAnswerValidationStep>();
        
        step.Setup(x => x.Priority)
            .Returns(1);

        step.Setup(x =>
                x.Evaluate(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .Returns((AnswerEvaluationResult?)null);
        
        var sut =
            CreateService(step.Object);
        
        var result =
            sut.EvaluateAnswer(
                "correct",
                "wrong");

        result.IsCorrect.ShouldBeFalse();
        result.Score.ShouldBe(0);
        result.Method.ShouldBe(EvaluationMethod.None);
    }
    
    [Fact]
    public void EvaluateAnswer_NoSteps_ReturnsNone()
    {
        var sut =
            CreateService();

        var result =
            sut.EvaluateAnswer(
                "correct",
                "wrong");

        result.IsCorrect.ShouldBeFalse();
        result.Score.ShouldBe(0);
        result.Method.ShouldBe(EvaluationMethod.None);
    }

    [Fact]
    public void EvaluateAnswer_MultipleSteps_UsesLowestPriorityFirst()
    {
        var first = new Mock<IAnswerValidationStep>();
        var second = new Mock<IAnswerValidationStep>();
        
        first.Setup(x => x.Priority)
            .Returns(5);

        second.Setup(x => x.Priority)
            .Returns(1);
        
        second.Setup(x =>
                x.Evaluate(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .Returns(new AnswerEvaluationResult
            {
                IsCorrect = true,
                Score = 100,
                Method = EvaluationMethod.Exact
            });
        
        var sut =
            CreateService(
                first.Object,
                second.Object);

        var result =
            sut.EvaluateAnswer(
                "a",
                "a");
        
        result.Method
            .ShouldBe(EvaluationMethod.Exact);
        
        second.Verify(x =>
            x.Evaluate(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
        
        first.Verify(x =>
            x.Evaluate(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }
}