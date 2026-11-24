using Domain.Utility.Result;
namespace Application.DTOs.Question;

public class AnswerEvaluationResult
{
    public bool IsCorrect { get; set; }
    public double Score { get; set; }
    public EvaluationMethod Method { get; set; }
    // The AI answer check would have run, but its quota is used up: the plain check decided
    public bool SmartCheckLimitReached { get; set; }
}