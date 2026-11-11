namespace Domain.Utility.Ai;

// Every paid AI function, quotas are counted per feature
public enum AiFeature
{
    QuestionGeneration = 1,
    DictionaryGeneration = 2,
    AnswerGrading = 3,
    SummaryGeneration = 4,
    MistakeExplanation = 5,
    // One selected word or phrase: translation, transcription and grammar. Short prompt, cheap
    WordLookup = 6,
    // Scores and corrections for one essay note
    EssayReview = 7,
    // Everyday examples for one dictionary word; nothing is saved
    WordExplanation = 8,
    // The script of an audio podcast of a material; the voices are paid separately
    PodcastGeneration = 9
}
