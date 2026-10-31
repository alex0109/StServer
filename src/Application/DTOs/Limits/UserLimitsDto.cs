using Domain.Utility.Subscription;

namespace Application.DTOs.Limits;

// The limits of the user's plan; null means no limit
public class UserLimitsDto
{
    public SubscriptionTier Tier { get; set; }
    public int? MaxMaterials { get; set; }
    public int? MaxTags { get; set; }
    public int? MaxQuestionsPerMaterial { get; set; }
    public int? MaxNotesPerMaterial { get; set; }
    public int? MaxDictionariesPerMaterial { get; set; }
    public int? MaxEntriesPerDictionary { get; set; }
    public int? MaxLibraryImports { get; set; }
    // When false, library copies don't take places of the user's own materials
    public bool CountLibraryImports { get; set; }
}
