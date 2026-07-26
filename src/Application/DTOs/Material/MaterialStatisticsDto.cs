using Domain.Utility.Material;

namespace Application.DTOs.Material;

public class MaterialStatisticsDto
{
    public int Count { get; set; }

    public Dictionary<MaterialStatus, int> Statuses { get; set; } = [];

    public Dictionary<MaterialType, int> Types { get; set; } = [];
}