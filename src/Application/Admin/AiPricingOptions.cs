namespace Application.Admin;

// Prices per million tokens in USD, to estimate what AI costs. Providers bill by their own
// records: these numbers are for the dashboard and must be updated when prices change
public class AiPricingOptions
{
    public const string SectionName = "AiPricing";

    // When the prices were last checked, shown next to the estimate
    public string? CheckedOn { get; set; }

    // Model id as stored in AiUsage.Model -> price
    public Dictionary<string, AiModelPrice> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Podcast voices, per million spoken characters. Not set: the voices are counted, not priced
    public decimal? SpeechPerMillionCharacters { get; set; }

    public decimal? SpeechCost(long characters) =>
        SpeechPerMillionCharacters is { } price ? characters / 1_000_000m * price : null;

    public decimal? Cost(string? model, long inputTokens, long outputTokens) =>
        model is not null && Models.TryGetValue(model, out var price)
            ? inputTokens / 1_000_000m * price.InputPerMillion + outputTokens / 1_000_000m * price.OutputPerMillion
            : null;
}

public class AiModelPrice
{
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
}
