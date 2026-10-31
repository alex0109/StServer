namespace Domain.Entities;

public class MaterialFile
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public Entities.Material Material { get; set; } = null!;

    public string StoragePath { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSize { get; set; }

    public DateTime CreatedAt { get; set; }
}