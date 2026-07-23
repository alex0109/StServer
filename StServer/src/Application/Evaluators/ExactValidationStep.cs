using StServer.Application.DTOs.Question;
using StServer.Application.Interfaces;

namespace StServer.Application.Evaluators;

public class ExactValidationStep : IAnswerValidationStep
{
    public int Priority => 1;
        
    public AnswerEvaluationResult? Evaluate(
        string correctAnswer,
        string userAnswer)
    {
        bool isCorrect = correctAnswer == userAnswer;

        if (isCorrect)
        {
            return new AnswerEvaluationResult
            {
                IsCorrect = true,
                Score = 100,
                Method = EvaluationMethod.Exact
            };
        }

        return null;
    }
}