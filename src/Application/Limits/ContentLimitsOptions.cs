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

// Rows in Postgres cost almost nothing, so the free plan is generous: tight limits hurt learning,
// not costs. AI is what costs money, and it has its own quotas
public static class ContentLimitDefaults
{
    public static readonly TierLimits Free = new(40, 50, 50, 30, 3, 500, 30);
    public static readonly TierLimits Plus = new(300, 200, 200, 100, 5, 3000, 300);
    public static readonly TierLimits Pro = new(null, null, null, null, null, null, null);

    public static TierLimits For(SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Plus => Plus,
        SubscriptionTier.Pro => Pro,
        _ => Free,
    };
}
