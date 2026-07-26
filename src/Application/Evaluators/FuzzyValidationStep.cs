using Application.DTOs.Question;
using Application.Interfaces;
using FuzzySharp;

namespace Application.Evaluators;

public class FuzzyValidationStep: IAnswerValidationStep
{
    public int Priority => 2;
    public AnswerEvaluationResult? Evaluate(string correctAnswer, string userAnswer)
    {
        int score = Fuzz.TokenSortRatio(
            correctAnswer.ToLowerInvariant(),
            userAnswer.ToLowerInvariant());

        if (score >= 85)
        {
            return new AnswerEvaluationResult
            {
                IsCorrect = true,
                Score = score,
                Method = EvaluationMethod.Fuzzy
            };
        }

        return null;
    }
}