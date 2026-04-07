namespace StServer;

public class MaterialItemDto
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Type { get; set; }
    public string[]? Tags { get; set; }
    public string? Link { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public MaterialItemDto() { }
    public MaterialItemDto(Material materialItem) =>
        (Id, Title, Type, Tags, Link, Description, Status, CreatedAt, UpdatedAt) = ( materialItem.Id, materialItem.Title, materialItem.Type, materialItem.Tags, materialItem.Link, materialItem.Description, materialItem.Status, materialItem.CreatedAt, materialItem.UpdatedAt);
}