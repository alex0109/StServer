namespace Application.DTOs.Limits;

// What the user already uses of every limit, counted the same way the limits are checked
public class LimitsUsageDto
{
    // Own materials; library copies are counted apart when they don't share the material limit
    public required int Materials { get; init; }
    public int LibraryCopies { get; init; }
    public required int Tags { get; init; }
    public required List<MaterialLimitsUsageDto> MaterialsUsage { get; init; }
}

public class MaterialLimitsUsageDto
{
    public required Guid MaterialId { get; init; }
    public required string Title { get; init; }
    public required int Questions { get; init; }
    public required int Notes { get; init; }
    public required int Dictionaries { get; init; }
    // Words of the fullest dictionary of the material
    public required int MaxDictionaryEntries { get; init; }
}
