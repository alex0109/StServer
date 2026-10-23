namespace Application.DTOs.Limits;

public class UserLimitsDto
{
    public int MaxMaterials { get; set; }
    public int MaxTags { get; set; }
    public int MaxQuestionsPerMaterial { get; set; }
    public int MaxNotesPerMaterial { get; set; }
    public int MaxDictionariesPerMaterial { get; set; }
    public int MaxEntriesPerDictionary { get; set; }
}
