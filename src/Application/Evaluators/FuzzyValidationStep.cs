using Domain.Utility.Result;
using Application.DTOs.Question;
using Application.Interfaces;

namespace Application.Evaluators;

public class FuzzyValidationStep: IAnswerValidationStep
{
    public int Priority => 2;

    public AnswerEvaluationResult? Evaluate(string correctAnswer, string userAnswer)
    {
        var match = FuzzyAnswerMatcher.Compare(correctAnswer, userAnswer);

        if (match.IsMatch)
        {
            return new AnswerEvaluationResult
            {
                IsCorrect = true,
                Score = match.Score,
                Method = EvaluationMethod.Fuzzy
            };
        }

        return null;
    }
}
