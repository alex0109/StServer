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

    public AnswerEvaluationResult EvaluateOptions(
        IReadOnlyCollection<Guid> correctOptionIds,
        IReadOnlyCollection<Guid>? userOptionIds)
    {
        var correct = correctOptionIds.ToHashSet();
        var chosen = (userOptionIds ?? []).ToHashSet();

        if (correct.Count == 0)
        {
            return new AnswerEvaluationResult
            {
                IsCorrect = false,
                Score = 0,
                Method = EvaluationMethod.Exact
            };
        }

        // Answer counts as correct only when exactly the correct options are chosen,
        // score gives partial credit and every wrong pick cancels one right pick
        bool isCorrect = chosen.SetEquals(correct);
        int rightPicks = chosen.Count(correct.Contains);
        int wrongPicks = chosen.Count - rightPicks;

        double score = Math.Max(0, rightPicks - wrongPicks) * 100.0 / correct.Count;

        return new AnswerEvaluationResult
        {
            IsCorrect = isCorrect,
            Score = Math.Round(score, 2),
            Method = EvaluationMethod.Exact
        };
    }
}