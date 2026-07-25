namespace Application.DTOs.Option;

public class OptionResponseDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsCorrect { get; set; }
}