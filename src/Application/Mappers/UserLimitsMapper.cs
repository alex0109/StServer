using Application.Constants;
using Application.DTOs.Limits;

namespace Application.Mappers;

public static class UserLimitsMapper
{
    public static UserLimitsDto ToDto()
    {
        return new UserLimitsDto
        {
            MaxMaterials = UserLimits.MaxMaterials,
            MaxTags = UserLimits.MaxTags,
            MaxQuestionsPerMaterial = UserLimits.MaxQuestionsPerMaterial,
            MaxNotesPerMaterial = UserLimits.MaxNotesPerMaterial,
            MaxDictionariesPerMaterial = UserLimits.MaxDictionariesPerMaterial,
            MaxEntriesPerDictionary = UserLimits.MaxEntriesPerDictionary,
        };
    }
}
