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

    public async Task<int?> GradeAsync(
        string question,
        string referenceAnswer,
        string userAnswer,
        CancellationToken cancellationToken = default)
    {
        // Grading is optional: without quota or providers the usual result stays
        var result = await _ai.TryRunAsync(
            new AnswerGradingPrompt(question, referenceAnswer, userAnswer),
            cancellationToken);

        return result?.Value;
    }
}
