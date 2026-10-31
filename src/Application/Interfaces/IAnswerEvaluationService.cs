using Application.DTOs.Question;

namespace Application.Interfaces;

public interface IAnswerEvaluationService
{
    AnswerEvaluationResult EvaluateAnswer(
        string correctAnswer, 
        string userAnswer);

    // Same as EvaluateAnswer, plus a meaning check for answers in the fuzzy "gray zone"
    Task<AnswerEvaluationResult> EvaluateAnswerAsync(
        string question,
        string correctAnswer,
        string userAnswer,
        CancellationToken cancellationToken = default);

    AnswerEvaluationResult EvaluateOptions(
        IReadOnlyCollection<Guid> correctOptionIds,
        IReadOnlyCollection<Guid>? userOptionIds);
}