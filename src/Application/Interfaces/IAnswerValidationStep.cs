using Application.DTOs.Question;

namespace Application.Interfaces;

public interface IAnswerValidationStep
{
    int Priority { get; }

    AnswerEvaluationResult? Evaluate(
        string correctAnswer,
        string userAnswer);
}