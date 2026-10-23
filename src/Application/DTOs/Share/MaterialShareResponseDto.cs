namespace Application.DTOs.Share;

public class MaterialShareResponseDto
{
    public Guid MaterialId { get; set; }
    public required string Token { get; set; }
    public int ImportsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
