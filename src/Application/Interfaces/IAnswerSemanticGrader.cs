namespace Application.Interfaces;

// Score 0-100, or null when the check didn't run; LimitReached: it didn't because the AI answer
// check quota is used up, which the app mentions with the plain result
public record SemanticGrade(int? Score, bool LimitReached = false);

// Checks the meaning of an open answer when string matching can't decide
public interface IAnswerSemanticGrader
{
    Task<SemanticGrade> GradeAsync(string question, string referenceAnswer, string userAnswer, CancellationToken cancellationToken = default);
}
