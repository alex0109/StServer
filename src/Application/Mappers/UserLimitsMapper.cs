using Application.DTOs.Limits;
using Application.Limits;
using Domain.Utility.Subscription;

namespace Application.Mappers;

public static class UserLimitsMapper
{
    public static UserLimitsDto ToDto(SubscriptionTier tier, TierLimits limits, bool countLibraryImports)
    {
        return new UserLimitsDto
        {
            Tier = tier,
            MaxMaterials = limits.Materials,
            MaxTags = limits.Tags,
            MaxQuestionsPerMaterial = limits.QuestionsPerMaterial,
            MaxNotesPerMaterial = limits.NotesPerMaterial,
            MaxDictionariesPerMaterial = limits.DictionariesPerMaterial,
            MaxEntriesPerDictionary = limits.EntriesPerDictionary,
            MaxLibraryImports = limits.LibraryImports,
            CountLibraryImports = countLibraryImports,
        };
    }
}
