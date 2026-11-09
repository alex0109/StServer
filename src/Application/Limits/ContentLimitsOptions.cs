using Domain.Utility.Subscription;

namespace Application.Limits;

// What a user can keep, per plan. null means no limit
public sealed record TierLimits(
    int? Materials,
    int? Tags,
    int? QuestionsPerMaterial,
    int? NotesPerMaterial,
    int? DictionariesPerMaterial,
    int? EntriesPerDictionary,
    int? LibraryImports);

// One plan in appsettings. A missing value keeps the default, -1 means no limit
public class TierLimitsConfig
{
    public int? Materials { get; set; }
    public int? Tags { get; set; }
    public int? QuestionsPerMaterial { get; set; }
    public int? NotesPerMaterial { get; set; }
    public int? DictionariesPerMaterial { get; set; }
    public int? EntriesPerDictionary { get; set; }
    public int? LibraryImports { get; set; }
}

public class ContentLimitsOptions
{
    public const string SectionName = "Limits";

    public Dictionary<SubscriptionTier, TierLimitsConfig>? Tiers { get; set; }

    // Off: library copies have their own limit and do not take places of the user's own materials
    public bool CountLibraryImports { get; set; }

    public TierLimits For(SubscriptionTier tier)
    {
        var defaults = ContentLimitDefaults.For(tier);
        if (Tiers is null || !Tiers.TryGetValue(tier, out var config)) return defaults;

        static int? Pick(int? configured, int? fallback) =>
            configured is null ? fallback : configured < 0 ? null : configured;

        return new TierLimits(
            Pick(config.Materials, defaults.Materials),
            Pick(config.Tags, defaults.Tags),
            Pick(config.QuestionsPerMaterial, defaults.QuestionsPerMaterial),
            Pick(config.NotesPerMaterial, defaults.NotesPerMaterial),
            Pick(config.DictionariesPerMaterial, defaults.DictionariesPerMaterial),
            Pick(config.EntriesPerDictionary, defaults.EntriesPerDictionary),
            Pick(config.LibraryImports, defaults.LibraryImports));
    }
}

// Content isn't what a plan sells: everyone has the same room, so nothing is locked when a
// subscription ends. The caps are far above real use and only keep scripts from filling the
// database. AI is what costs money, and its quotas are what the plans change
public static class ContentLimitDefaults
{
    public static readonly TierLimits Everyone = new(1000, 300, 500, 300, 20, 5000, 300);

    public static TierLimits For(SubscriptionTier tier) => Everyone;
}
