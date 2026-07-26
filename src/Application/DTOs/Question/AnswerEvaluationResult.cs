namespace Application.DTOs.Question;

public class AnswerEvaluationResult
{
    public bool IsCorrect { get; set; }

    public double Score { get; set; }

    public EvaluationMethod Method { get; set; }
}