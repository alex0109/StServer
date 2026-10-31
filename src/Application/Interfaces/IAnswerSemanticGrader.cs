namespace Application.Interfaces;

// Checks the meaning of an open answer when string matching can't decide
public interface IAnswerSemanticGrader
{
    // Score 0-100, or null when the check is not available right now
    Task<int?> GradeAsync(string question, string referenceAnswer, string userAnswer, CancellationToken cancellationToken = default);
}
