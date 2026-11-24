using Application.Exceptions;
using Application.Ai.Prompts;
using Application.Interfaces;

namespace Application.Services.Ai;

public class AiAnswerGrader : IAnswerSemanticGrader
{
    private readonly IAiService _ai;

    public AiAnswerGrader(IAiService ai)
    {
        _ai = ai;
    }

    public async Task<SemanticGrade> GradeAsync(
        string question,
        string referenceAnswer,
        string userAnswer,
        CancellationToken cancellationToken = default)
    {
        // Grading is optional: over the quota or without providers the plain check stays, and the
        // answer check is never sold for credits
        try
        {
            var result = await _ai.RunAsync(new AnswerGradingPrompt(question, referenceAnswer, userAnswer), cancellationToken);
            return new SemanticGrade(result.Value);
        }
        catch (AiUsageDeniedException)
        {
            return new SemanticGrade(null, LimitReached: true);
        }
        catch (AiUnavailableException)
        {
            return new SemanticGrade(null);
        }
    }
}
