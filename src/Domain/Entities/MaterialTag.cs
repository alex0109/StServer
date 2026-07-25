namespace StServer.Domain.Entities;

public class MaterialTag
{
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}