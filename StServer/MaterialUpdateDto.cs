namespace StServer;

public class MaterialUpdateDto
{
    public string? Title { get; set; }
    public string? Type { get; set; }
    public string[]? Tags { get; set; }
    public string? Link { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}