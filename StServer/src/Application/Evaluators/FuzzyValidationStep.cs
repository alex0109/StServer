using StServer.Application.DTOs.Question;
using StServer.Application.Interfaces;
using FuzzySharp;

namespace StServer.Application.Evaluators;

public class FuzzyValidationStep: IAnswerValidationStep
{
    public int Priority => 2;
    public AnswerEvaluationResult? Evaluate(string correctAnswer, string userAnswer)
    {
        int score = Fuzz.TokenSortRatio(correctAnswer, userAnswer);

        if (score >= 95)
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