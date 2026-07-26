using Application.DTOs.Question;
using Application.Interfaces;

namespace Application.Services;

public class AnswerEvaluationService : IAnswerEvaluationService
{
    private readonly IAnswerNormalizer _normalizer;
    private readonly IEnumerable<IAnswerValidationStep> _steps;

    public AnswerEvaluationService(
        IAnswerNormalizer normalizer,
        IEnumerable<IAnswerValidationStep> steps)
    {
        _normalizer = normalizer;
        _steps = steps;
    }

    public AnswerEvaluationResult EvaluateAnswer(
        string correctAnswer,
        string userAnswer)
    {
        correctAnswer = _normalizer.Normalize(correctAnswer);
        userAnswer = _normalizer.Normalize(userAnswer);

        foreach(var step in _steps.OrderBy(x => x.Priority))
        {
            var result = step.Evaluate(
                correctAnswer,
                userAnswer);

            if (result != null)
                return result;
        }

        return new AnswerEvaluationResult
        {
            IsCorrect = false,
            Score = 0,
            Method = EvaluationMethod.None
        };
    }
}