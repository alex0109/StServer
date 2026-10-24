namespace Domain.Utility.Ai;

// Every paid AI function, quotas are counted per feature
public enum AiFeature
{
    QuestionGeneration = 1,
    DictionaryGeneration = 2,
    AnswerGrading = 3,
    SummaryGeneration = 4,
    MistakeExplanation = 5
}
