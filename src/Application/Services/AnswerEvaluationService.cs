using Domain.Utility.Result;
using Application.Ai;
using Application.DTOs.Question;
using Application.Evaluators;
using Application.Interfaces;

namespace Application.Services;

public class AnswerEvaluationService : IAnswerEvaluationService
{
    private readonly IAnswerNormalizer _normalizer;
    private readonly IEnumerable<IAnswerValidationStep> _steps;
    private readonly IAnswerSemanticGrader? _semanticGrader;
    private readonly AiOptions _aiOptions;

    public AnswerEvaluationService(
        IAnswerNormalizer normalizer,
        IEnumerable<IAnswerValidationStep> steps,
        IAnswerSemanticGrader? semanticGrader = null,
        AiOptions? aiOptions = null)
    {
        _normalizer = normalizer;
        _steps = steps;
        _semanticGrader = semanticGrader;
        _aiOptions = aiOptions ?? new AiOptions();
    }

    public async Task<AnswerEvaluationResult> EvaluateAnswerAsync(
        string question,
        string correctAnswer,
        string userAnswer,
        CancellationToken cancellationToken = default)
    {
        var result = EvaluateAnswer(correctAnswer, userAnswer);

        if (result.IsCorrect || _semanticGrader is null)
            return result;

        var normalizedUserAnswer = _normalizer.Normalize(userAnswer);
        if (normalizedUserAnswer.Length == 0)
            return result;

        // Clearly right answers already passed fuzzy matching, clearly wrong ones are too far to be
        // worth a paid check. A close answer with another number or a negation is exactly the doubtful case
        var fuzzy = FuzzyAnswerMatcher.Compare(_normalizer.Normalize(correctAnswer), normalizedUserAnswer);
        if (fuzzy.Score < _aiOptions.GradingGrayZoneMin)
            return result;

        var aiScore = await _semanticGrader.GradeAsync(question, correctAnswer, userAnswer, cancellationToken);

        // Grading must never break answering: without AI the usual result stays
        if (aiScore is null)
            return result;

        return new AnswerEvaluationResult
        {
            IsCorrect = aiScore >= _aiOptions.GradingPassScore,
            Score = aiScore.Value,
            Method = EvaluationMethod.AI
        };
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