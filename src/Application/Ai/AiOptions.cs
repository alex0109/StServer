using Domain.Utility.Ai;
using Domain.Utility.Subscription;

namespace Application.Ai;

// Rules of AI usage that do not depend on a provider
public class AiOptions
{
    public const string SectionName = "Ai";

    // Kill switch: all AI features answer "unavailable" when false
    public bool Enabled { get; set; } = true;

    // Providers are tried in this order until one answers, names match IAiProvider.Name
    public string[]? ProviderOrder { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 120;

    // Fuzzy score from this value up to the fuzzy pass score is the "gray zone" checked by AI
    public int GradingGrayZoneMin { get; set; } = 40;
    // AI score from this value counts as a correct answer
    public int GradingPassScore { get; set; } = 70;

    // Monthly requests per tier and feature, missing values use AiQuotaDefaults
    public Dictionary<SubscriptionTier, Dictionary<AiFeature, int>>? Quotas { get; set; }

    public static readonly string[] DefaultProviderOrder = ["Anthropic", "OpenAi", "Gemini"];

    public int GetMonthlyQuota(SubscriptionTier tier, AiFeature feature)
    {
        if (Quotas is not null
            && Quotas.TryGetValue(tier, out var features)
            && features.TryGetValue(feature, out var quota))
        {
            return quota;
        }

        return AiQuotaDefaults.Get(tier, feature);
    }
}

public static class AiQuotaDefaults
{
    private static readonly Dictionary<SubscriptionTier, Dictionary<AiFeature, int>> Values = new()
    {
        [SubscriptionTier.Free] = new()
        {
            [AiFeature.QuestionGeneration] = 10,
            [AiFeature.DictionaryGeneration] = 5,
            [AiFeature.AnswerGrading] = 30,
            [AiFeature.SummaryGeneration] = 3,
            [AiFeature.MistakeExplanation] = 5,
        },
        [SubscriptionTier.Plus] = new()
        {
            [AiFeature.QuestionGeneration] = 100,
            [AiFeature.DictionaryGeneration] = 50,
            [AiFeature.AnswerGrading] = 300,
            [AiFeature.SummaryGeneration] = 30,
            [AiFeature.MistakeExplanation] = 50,
        },
        [SubscriptionTier.Pro] = new()
        {
            [AiFeature.QuestionGeneration] = 500,
            [AiFeature.DictionaryGeneration] = 250,
            [AiFeature.AnswerGrading] = 1500,
            [AiFeature.SummaryGeneration] = 150,
            [AiFeature.MistakeExplanation] = 250,
        },
    };

    public static int Get(SubscriptionTier tier, AiFeature feature) =>
        Values.TryGetValue(tier, out var features) && features.TryGetValue(feature, out var quota) ? quota : 0;
}
