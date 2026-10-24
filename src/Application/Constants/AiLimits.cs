namespace Application.Constants;

public class AiLimits
{
    public const int MaxGeneratedQuestions = 5;
    public const int MaxGeneratedEntries = 20;
    // Shorter sources give the model nothing to work with
    public const int MinSourceLength = 50;
    public const int MaxExplainedMistakes = 20;
}
