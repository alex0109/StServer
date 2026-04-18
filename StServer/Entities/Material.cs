namespace StServer.Entities;

public class Material
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
}