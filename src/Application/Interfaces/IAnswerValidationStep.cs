using StServer.Application.DTOs.Question;

namespace StServer.Application.Interfaces;

public interface IAnswerValidationStep
{
    int Priority { get; }

    AnswerEvaluationResult? Evaluate(
        string correctAnswer,
        string userAnswer);
}