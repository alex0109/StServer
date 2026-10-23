using Application.DTOs.Question;

namespace Application.Interfaces;

public interface IAnswerEvaluationService
{
    AnswerEvaluationResult EvaluateAnswer(
        string correctAnswer, 
        string userAnswer);

    AnswerEvaluationResult EvaluateOptions(
        IReadOnlyCollection<Guid> correctOptionIds,
        IReadOnlyCollection<Guid>? userOptionIds);
}