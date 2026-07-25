using StServer.Application.DTOs.Question;

namespace StServer.Application.Interfaces;

public interface IAnswerEvaluationService
{
    AnswerEvaluationResult EvaluateAnswer(
        string correctAnswer, 
        string userAnswer);
}